using System.Text.Json;

namespace DynamicIsland.Core;

public sealed record QuickActionConfigItem(string Id, bool Enabled = true);

/// <summary>
/// Pengaturan aksi cepat yang bisa diatur sendiri oleh pengguna (Tahap 3).
/// Tersimpan di %APPDATA%\DynamicIsland\quick-actions.json.
/// </summary>
public static class QuickActionCatalog
{
    public static readonly IReadOnlyList<string> AllKnownActionIds = new[]
    {
        "media-toggle",
        "media-next",
        "timer-focus",
        "timer-stopwatch",
        "screenshot",
        "camera",
        "notes",
        "clipboard",
    };

    public static readonly IReadOnlyList<string> DefaultActionIds = new[]
    {
        "media-toggle",
        "media-next",
        "timer-focus",
    };

    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DefaultActionIds;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            if (parsed is null || parsed.Count == 0) return DefaultActionIds;

            var filtered = parsed.Where(id => AllKnownActionIds.Contains(id, StringComparer.OrdinalIgnoreCase)).Distinct().ToList();
            return filtered.Count > 0 ? filtered : DefaultActionIds;
        }
        catch (JsonException)
        {
            return DefaultActionIds;
        }
    }

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Serialize(IEnumerable<string> ids) =>
        JsonSerializer.Serialize(ids.Distinct(), WriteOptions);
}

public sealed class QuickActionStore
{
    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_QUICKACTIONS") is { Length: > 0 } custom
        ? custom
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "quick-actions.json");

    public string FilePath { get; }
    public List<string> ActionIds { get; } = new();

    public QuickActionStore(string? path = null)
    {
        FilePath = path ?? DefaultPath;
        ActionIds.AddRange(QuickActionCatalog.DefaultActionIds);
    }

    public bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;

            var ids = QuickActionCatalog.Parse(File.ReadAllText(FilePath));
            ActionIds.Clear();
            ActionIds.AddRange(ids);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(FilePath, QuickActionCatalog.Serialize(ActionIds));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
