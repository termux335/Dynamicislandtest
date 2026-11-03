using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland.Core;

/// <summary>
/// Profil per aplikasi/game (Tahap 4): posisi terakhir, ukuran, opacity, dan
/// tingkat respons yang dipilih pengguna.
/// </summary>
public sealed record AppProfile(
    string Process,
    SafePosition Position = SafePosition.TopCenter,
    DisplayMode Mode = DisplayMode.Standar,
    double Opacity = 1,
    ResponseLevel Level = ResponseLevel.Normal,
    DateTimeOffset SavedAt = default);

/// <summary>
/// Menyimpan profil aplikasi di berkas JSON lokal. Berkas rusak atau tidak bisa
/// dibaca tidak pernah menggagalkan app: profil kembali kosong.
/// </summary>
public sealed class AppProfileStore
{
    private const int MaxProfiles = 50;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<AppProfile> profiles = new();

    public AppProfileStore(string? path = null) => FilePath = path ?? DefaultPath;

    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_APP_PROFILES") is { Length: > 0 } custom
        ? custom
        : System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "app-profiles.json");

    public string FilePath { get; }

    public IReadOnlyList<AppProfile> All => profiles;

    public AppProfile? For(string? process) =>
        string.IsNullOrWhiteSpace(process)
            ? null
            : profiles.FirstOrDefault(profile => string.Equals(profile.Process, process, StringComparison.OrdinalIgnoreCase));

    /// <summary>Mengingat profil terbaru; yang paling baru dipakai lebih dulu.</summary>
    public void Remember(AppProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Process)) return;

        profiles.RemoveAll(existing => string.Equals(existing.Process, profile.Process, StringComparison.OrdinalIgnoreCase));
        profiles.Insert(0, profile);

        if (profiles.Count > MaxProfiles) profiles.RemoveRange(MaxProfiles, profiles.Count - MaxProfiles);
    }

    /// <summary>Membaca berkas. False bila berkas tidak ada atau rusak.</summary>
    public bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;

            var loaded = JsonSerializer.Deserialize<List<AppProfile>>(File.ReadAllText(FilePath), Options);
            if (loaded is null) return false;

            profiles.Clear();
            profiles.AddRange(loaded.Where(profile => !string.IsNullOrWhiteSpace(profile.Process)).Take(MaxProfiles));
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

            File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, Options));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
