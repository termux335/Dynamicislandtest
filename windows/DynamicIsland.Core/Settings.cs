using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland.Core;

/// <summary>Preferensi yang bertahan antar sesi.</summary>
/// <param name="Humor">
/// Kepribadian teks. Bawaan kocak sesuai <c>CLAUDE.windows.md</c>; onboarding
/// nanti menanyakan sekali, dan menu klik kanan selalu bisa mengubahnya.
/// </param>
public sealed record IslandSettings(
    DisplayMode DisplayMode = DisplayMode.Standar,
    bool ShowsSecondary = false,
    bool AutoPlacement = true,
    Language Language = Language.Id,
    HumorMode Humor = HumorMode.Kocak,
    AccentChoice Accent = AccentChoice.Oranye,
    PositionChoice Position = PositionChoice.TopCenter,
    PriorityPreset Priority = PriorityPreset.Bawaan,
    bool NotificationsEnabled = false,
    bool StartWithWindows = false,
    bool SafeShare = true,
    bool OledProtection = true,
    bool PrayerEnabled = false,
    bool WeatherEnabled = false,
    string? WeatherAdm4 = null,
    bool HabitsEnabled = false,
    bool StudyMode = false,
    bool ClipboardEnabled = false,
    bool PaletteEnabled = true,
    bool OsdEnabled = false,
    bool MirrorEnabled = false,
    ContextAwarenessSettings? Context = null,
    Stage5BSettings? Stage5B = null,
    Stage5CSettings? Stage5C = null,
    HumorSettings? HumorBehaviors = null);

/// <summary>
/// Menyimpan preferensi ke satu berkas JSON. Berkas hilang atau rusak tidak
/// pernah menggagalkan app: nilainya kembali ke bawaan.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Lokasi bawaan: <c>%APPDATA%\DynamicIsland\settings.json</c>, atau
    /// <c>ISLAND_SETTINGS</c> bila diisi (untuk uji manual dengan berkas bersih).
    /// </summary>
    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_SETTINGS") is { Length: > 0 } custom
        ? custom
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "settings.json");

    /// <summary>True bila berkas pengaturan sudah ada: dipakai untuk tahu ini jalan pertama.</summary>
    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static IslandSettings Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file)) return new IslandSettings();
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<IslandSettings>(json, Options) ?? new IslandSettings();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new IslandSettings();
        }
    }

    /// <summary>Menyimpan preferensi. Mengembalikan false bila berkas tidak bisa ditulis.</summary>
    public static bool Save(IslandSettings settings, string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(file, JsonSerializer.Serialize(settings, Options));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
