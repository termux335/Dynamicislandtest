using System.Globalization;
using System.Text.Json;

namespace DynamicIsland.Core;

/// <summary>Bahasa antarmuka. Hanya dua berkas string yang dibawa app.</summary>
public enum Language
{
    Id,
    En,
}

/// <summary>
/// Dua kepribadian teks (lihat <c>CLAUDE.windows.md</c>): serius dan kocak.
/// Teks penting tetap serius; humor hanya tambahan di sampingnya.
/// </summary>
public enum HumorMode
{
    Serius,
    Kocak,
}

/// <summary>
/// Satu berkas string (<c>strings-indonesia.json</c> / <c>strings-english.json</c>).
///
/// Setiap kunci punya varian <c>serius</c> dan <c>kocak</c>. Bila varian kocak
/// kosong, teks serius dipakai; bila kuncinya tidak ada sama sekali, kunci itu
/// sendiri dikembalikan supaya teks yang lupa diterjemahkan terlihat, bukan
/// hilang diam-diam.
/// </summary>
public sealed class StringCatalog
{
    private const string ResourcePrefix = "DynamicIsland.Core.strings.";

    private readonly Dictionary<string, Entry> entries;

    private StringCatalog(Language language, Dictionary<string, Entry> entries)
    {
        Language = language;
        this.entries = entries;
    }

    private readonly record struct Entry(string? Serius, string? Kocak);

    public Language Language { get; }

    public int Count => entries.Count;

    public IReadOnlyCollection<string> Keys => entries.Keys;

    /// <summary>Kunci yang tidak punya teks serius: pelanggaran aturan, perlu ditambal.</summary>
    public IReadOnlyList<string> MissingSerious =>
        entries.Where(pair => string.IsNullOrWhiteSpace(pair.Value.Serius)).Select(pair => pair.Key).OrderBy(key => key).ToList();

    /// <summary>Kunci yang tidak punya padanan kocak (dianggap wajar untuk teks teknis).</summary>
    public IReadOnlyList<string> MissingKocak =>
        entries.Where(pair => string.IsNullOrWhiteSpace(pair.Value.Kocak)).Select(pair => pair.Key).OrderBy(key => key).ToList();

    public bool Has(string key) => entries.ContainsKey(key);

    /// <summary>Teks untuk satu kunci. Kunci tak dikenal dikembalikan apa adanya.</summary>
    public string Get(string key, HumorMode humor = HumorMode.Serius)
    {
        if (!entries.TryGetValue(key, out var entry)) return key;

        var chosen = humor == HumorMode.Kocak ? entry.Kocak : entry.Serius;
        if (string.IsNullOrWhiteSpace(chosen)) chosen = entry.Serius;
        return string.IsNullOrWhiteSpace(chosen) ? key : chosen;
    }

    /// <summary>Seperti <see cref="Get"/>, lalu diisi dengan <c>string.Format</c>.</summary>
    public string Format(string key, HumorMode humor = HumorMode.Serius, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key, humor), args);

    /// <summary>Membaca katalog dari teks JSON. Bentuk: <c>{ "kunci": { "serius": "...", "kocak": "..." } }</c>.</summary>
    public static StringCatalog Parse(Language language, string json)
    {
        using var document = JsonDocument.Parse(json);

        var parsed = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Kunci \"{property.Name}\" harus berupa objek dengan serius/kocak.");
            }

            parsed[property.Name] = new Entry(
                Text(property.Value, "serius"),
                Text(property.Value, "kocak"));
        }

        return new StringCatalog(language, parsed);
    }

    /// <summary>Katalog yang ditanam di assembly Core.</summary>
    public static StringCatalog Load(Language language)
    {
        var name = ResourcePrefix + (language == Language.En ? "english.json" : "indonesia.json");
        var assembly = typeof(StringCatalog).Assembly;

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Berkas string {name} tidak ditemukan di assembly.");

        using var reader = new StreamReader(stream);
        return Parse(language, reader.ReadToEnd());
    }

    private static string? Text(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// Katalog bahasa + kepribadian yang sedang aktif. Satu instance dipakai bersama
/// oleh lapisan UI dan monitor supaya pergantian bahasa atau Mode Kocak langsung
/// berlaku tanpa restart.
/// </summary>
public sealed class IslandText
{
    private static readonly Lazy<StringCatalog> Indonesian = new(() => StringCatalog.Load(Language.Id));
    private static readonly Lazy<StringCatalog> English = new(() => StringCatalog.Load(Language.En));

    /// <summary>Acuan netral untuk tes dan pemanggil yang tidak peduli bahasa: Indonesia + serius.</summary>
    public static IslandText Default { get; } = new(Language.Id, HumorMode.Serius);

    public IslandText(Language language = Language.Id, HumorMode humor = HumorMode.Serius)
    {
        Language = language;
        Humor = humor;
    }

    public Language Language { get; private set; }

    public HumorMode Humor { get; private set; }

    public StringCatalog Catalog => For(Language);

    /// <summary>Katalog untuk satu bahasa; dimuat sekali per proses.</summary>
    public static StringCatalog For(Language language) =>
        language == Language.En ? English.Value : Indonesian.Value;

    /// <summary>Mengganti bahasa dan/atau kepribadian di tempat.</summary>
    public void Use(Language language, HumorMode humor)
    {
        Language = language;
        Humor = humor;
    }

    public string Get(string key) => Catalog.Get(key, Humor);

    /// <summary>Selalu versi serius: dipakai pembaca layar (aturan humor nomor 3).</summary>
    public string GetSerious(string key) => Catalog.Get(key, HumorMode.Serius);

    public string Format(string key, params object?[] args) => Catalog.Format(key, Humor, args);

    public string FormatSerious(string key, params object?[] args) =>
        Catalog.Format(key, HumorMode.Serius, args);
}
