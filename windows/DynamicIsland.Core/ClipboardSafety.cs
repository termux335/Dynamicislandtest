using System.Text.RegularExpressions;

namespace DynamicIsland.Core;

public enum ClipboardKind
{
    Text,
    Link,
    Image,
}

/// <summary>
/// Satu entri riwayat clipboard. Teks sensitif sudah disamarkan sebelum
/// disimpan, jadi nilai aslinya tidak pernah masuk riwayat.
/// </summary>
public sealed record ClipboardItem(
    ClipboardKind Kind,
    string Text,
    DateTimeOffset CapturedAt,
    bool Redacted = false)
{
    /// <summary>Label pendek untuk daftar palette.</summary>
    public string Label => Text.Length <= 80 ? Text : Text[..77] + "…";
}

/// <summary>
/// Aturan penyamaran data sensitif (Tahap 5a).
///
/// Yang disamarkan: nomor kartu yang lolos Luhn, baris berlabel password/token/
/// API key/OTP/PIN, JWT, dan string acak panjang tanpa spasi. Nomor kartu tetap
/// menyimpan empat digit terakhir supaya masih bisa dikenali.
///
/// Semua berjalan lokal; tidak ada data yang dikirim ke mana pun.
/// </summary>
public static class ClipboardSafety
{
    public const string Mask = "••••••";

    private static readonly Regex CardPattern = new(@"\b(?:\d[ -]?){12,18}\d\b", RegexOptions.Compiled);

    private static readonly Regex TokenPattern = new(
        @"(?i)(api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|secret|password|passwd|pwd|otp|pin|bearer)\s*[:=]\s*\S+",
        RegexOptions.Compiled);

    private static readonly Regex JwtPattern = new(
        @"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b",
        RegexOptions.Compiled);

    /// <summary>Menyamarkan teks sebelum disimpan. True berarti ada yang disamarkan.</summary>
    public static (string Text, bool Redacted) Sanitize(string? text, ClipboardKind kind)
    {
        if (string.IsNullOrWhiteSpace(text)) return (string.Empty, false);

        var value = text.Trim();

        // Tautan disimpan apa adanya supaya masih bisa dibuka. Tanda tangan di
        // dalam URL bukan sesuatu yang bisa dicari polanya dengan andal.
        if (kind == ClipboardKind.Link || IsLink(value)) return (value, false);

        var redacted = false;
        var maskedCards = CardPattern.Replace(value, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is < 13 or > 19 || !PassesLuhn(digits)) return match.Value;

            redacted = true;
            return "•••• " + digits[^4..];
        });

        if (TokenPattern.IsMatch(maskedCards) || JwtPattern.IsMatch(maskedCards) || LooksRandom(maskedCards))
        {
            return (Mask, true);
        }

        return (maskedCards, redacted);
    }

    /// <summary>True bila teks tampak seperti rahasia yang harus disamarkan seluruhnya.</summary>
    public static bool IsSensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        return TokenPattern.IsMatch(text) || JwtPattern.IsMatch(text) || LooksRandom(text);
    }

    public static bool LooksLikeCardNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return digits.Length is >= 13 and <= 19 && PassesLuhn(digits);
    }

    /// <summary>Nomor kartu valid menurut algoritma Luhn.</summary>
    public static bool PassesLuhn(string digits)
    {
        if (digits.Length == 0 || digits.Any(character => character is < '0' or > '9')) return false;

        var sum = 0;
        var alternate = false;

        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var value = digits[index] - '0';

            if (alternate)
            {
                value *= 2;
                if (value > 9) value -= 9;
            }

            sum += value;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    public static bool IsLink(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>String acak panjang tanpa spasi: indikasi token atau kunci.</summary>
    private static bool LooksRandom(string text)
    {
        if (text.Length < 24 || text.Any(char.IsWhiteSpace)) return false;

        var hasLetter = text.Any(char.IsLetter);
        var hasDigit = text.Any(char.IsDigit);
        var hasSymbol = text.Any(character => character is '-' or '_' or '+' or '/');

        return hasLetter && hasDigit && hasSymbol;
    }
}

/// <summary>
/// Riwayat clipboard di memori dengan batas jumlah dan umur.
///
/// Tidak ada penulisan ke cakram. Entri yang lebih tua dari masa simpan dicabut
/// saat <see cref="Purge"/> dipanggil, dan seluruh riwayat bisa dihapus kapan saja.
/// </summary>
public sealed class ClipboardHistory
{
    private readonly List<ClipboardItem> items = new();
    private readonly Func<DateTimeOffset> now;

    public ClipboardHistory(int capacity = 30, TimeSpan? retention = null, Func<DateTimeOffset>? now = null)
    {
        Capacity = Math.Max(1, capacity);
        Retention = retention ?? TimeSpan.FromHours(24);
        this.now = now ?? (() => DateTimeOffset.Now);
    }

    public int Capacity { get; }

    public TimeSpan Retention { get; }

    public IReadOnlyList<ClipboardItem> Items => items;

    /// <summary>
    /// Menambahkan teks/gambar. Nilai sensitif disamarkan lebih dulu, duplikat
    /// berurutan diabaikan, dan entri terlama dibuang saat kapasitas penuh.
    /// </summary>
    public ClipboardItem? Add(ClipboardKind kind, string? text, DateTimeOffset? capturedAt = null)
    {
        var (sanitized, redacted) = ClipboardSafety.Sanitize(text, kind);
        if (string.IsNullOrWhiteSpace(sanitized)) return null;

        var at = capturedAt ?? now();
        Purge(at);

        if (items.Count > 0 && items[0].Text == sanitized && items[0].Kind == kind) return null;

        var item = new ClipboardItem(kind, sanitized, at, redacted);
        items.Insert(0, item);

        if (items.Count > Capacity) items.RemoveRange(Capacity, items.Count - Capacity);

        return item;
    }

    public IReadOnlyList<ClipboardItem> Search(string? query, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(query)) return items.Take(limit).ToList();

        return items
            .Where(item => item.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    /// <summary>Mencabut entri yang lebih tua dari masa simpan; mengembalikan jumlahnya.</summary>
    public int Purge(DateTimeOffset? at = null)
    {
        var limit = (at ?? now()) - Retention;
        return items.RemoveAll(item => item.CapturedAt < limit);
    }

    public void Clear() => items.Clear();
}
