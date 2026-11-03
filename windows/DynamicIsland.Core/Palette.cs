using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland.Core;

/// <summary>Pintasan aplikasi/folder yang didaftarkan sendiri oleh pengguna (Tahap 5a).</summary>
public sealed record Shortcut(string Id, string Label, string Path, bool IsFolder = false);

/// <summary>Jenis hasil di command palette.</summary>
public enum PaletteKind
{
    Calculation,
    Shortcut,
    Clipboard,
    Command,
}

public sealed record PaletteEntry(string Id, string Title, string Subtitle, PaletteKind Kind, string Payload);

/// <summary>Label palette yang mengikuti bahasa aktif.</summary>
public sealed record PaletteLabels(
    string Copy = "Salin hasil",
    string Folder = "Folder",
    string App = "Aplikasi",
    string Clipboard = "Clipboard");

/// <summary>
/// Menyusun daftar hasil command palette dari satu query: hitungan, pintasan,
/// dan riwayat clipboard. Isi clipboard sudah disamarkan sebelum sampai sini.
/// </summary>
public static class Palette
{
    public static IReadOnlyList<PaletteEntry> Build(
        string? query,
        IEnumerable<Shortcut>? shortcuts = null,
        IEnumerable<ClipboardItem>? clipboard = null,
        PaletteLabels? labels = null,
        int limit = 8)
    {
        var text = query?.Trim() ?? string.Empty;
        var entries = new List<PaletteEntry>();
        labels ??= new PaletteLabels();

        if (text.Length > 0 && Calculator.LooksLikeExpression(text) && Calculator.TryCalculate(text, out var display))
        {
            entries.Add(new PaletteEntry("calc", display, labels.Copy, PaletteKind.Calculation, display));
        }

        var shortcutList = shortcuts?.ToList() ?? new List<Shortcut>();
        foreach (var shortcut in shortcutList)
        {
            if (text.Length > 0 &&
                !shortcut.Label.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !text.Contains(shortcut.Label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(new PaletteEntry(
                "shortcut:" + shortcut.Id,
                shortcut.Label,
                shortcut.IsFolder ? labels.Folder : labels.App,
                PaletteKind.Shortcut,
                shortcut.Path));
        }

        if (text.Length > 0 && clipboard is not null)
        {
            foreach (var item in clipboard)
            {
                if (!item.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;

                entries.Add(new PaletteEntry(
                    "clipboard:" + item.CapturedAt.UtcTicks,
                    item.Label,
                    $"{labels.Clipboard} · {item.CapturedAt.ToLocalTime():HH:mm}",
                    PaletteKind.Clipboard,
                    item.Text));
            }
        }

        return entries.Take(Math.Max(1, limit)).ToList();
    }
}

/// <summary>
/// Daftar pintasan yang didaftarkan pengguna, disimpan sebagai JSON lokal.
/// Tidak ada eksekusi dari sumber luar: hanya path/URL yang ditulis sendiri.
/// </summary>
public sealed class ShortcutStore
{
    private const int MaxShortcuts = 24;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<Shortcut> items = new();

    public ShortcutStore(string? path = null) => FilePath = path ?? DefaultPath;

    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_SHORTCUTS") is { Length: > 0 } custom
        ? custom
        : System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "shortcuts.json");

    public string FilePath { get; }

    public IReadOnlyList<Shortcut> All => items;

    /// <summary>Menambah atau mengganti pintasan dengan path yang sama.</summary>
    public bool Remember(Shortcut shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut.Label) || string.IsNullOrWhiteSpace(shortcut.Path)) return false;

        items.RemoveAll(item => string.Equals(item.Path, shortcut.Path, StringComparison.OrdinalIgnoreCase));

        items.Insert(0, shortcut with
        {
            Id = string.IsNullOrWhiteSpace(shortcut.Id) ? Guid.NewGuid().ToString("N")[..8] : shortcut.Id,
        });

        if (items.Count > MaxShortcuts) items.RemoveRange(MaxShortcuts, items.Count - MaxShortcuts);
        return true;
    }

    public bool Remove(string id) => items.RemoveAll(item => item.Id == id) > 0;

    public bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;

            var loaded = JsonSerializer.Deserialize<List<Shortcut>>(File.ReadAllText(FilePath), Options);
            if (loaded is null) return false;

            items.Clear();
            items.AddRange(loaded
                .Where(item => !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.Path))
                .Take(MaxShortcuts));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public bool Save()
    {
        try
        {
            var folder = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(items, Options));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
