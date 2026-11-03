using System.Text.Json;
using System.Xml.Linq;

namespace DynamicIsland.Core;

/// <summary>
/// Preferensi modul Tahap 5c. Fitur yang butuh internet atau data sensitif
/// selalu mati secara bawaan.
/// </summary>
public sealed record Stage5CSettings(
    bool AgentStatusEnabled = false,
    bool TerminalEnabled = false,
    bool ScreenshotEnabled = true,
    bool TickerEnabled = false,
    string? TickerUrl = null,
    bool AssistantEnabled = false,
    string? AssistantEndpoint = null,
    string? AssistantModel = null,
    bool PluginsEnabled = false);

// --- Status agen AI coding (5c.13) ---

/// <summary>Satu permintaan izin dari alat agen yang dibaca dari berkas lokal.</summary>
public sealed record AgentRequest(string Id, string Summary, string? Detail = null);

/// <summary>Status alat agen; tidak pernah memakai kunci API atau jaringan.</summary>
public sealed record AgentSnapshot(
    string Tool,
    string State,
    int? Used = null,
    int? Limit = null,
    AgentRequest? Request = null,
    DateTimeOffset? UpdatedAt = null);

/// <summary>
/// Membaca status agen dari JSON lokal yang ditulis alat pengguna sendiri.
/// Format longgar supaya alat berbeda bisa mengisi sebagian saja.
/// </summary>
public static class AgentStatusFile
{
    public const string ResponseName = "agent-response.json";

    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_AGENT_STATUS") is { Length: > 0 } custom
        ? custom
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "agent-status.json");

    public static string ResponsePathFor(string statusPath) =>
        Path.Combine(Path.GetDirectoryName(statusPath) ?? ".", ResponseName);

    public static AgentSnapshot? Read(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file)) return null;

            return AgentStatusParser.Parse(File.ReadAllText(file));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Menulis jawaban setuju/tolak ke berkas yang dibaca alat agen.</summary>
    public static bool Respond(string statusPath, string requestId, bool approved)
    {
        try
        {
            var file = ResponsePathFor(statusPath);
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(file, JsonSerializer.Serialize(new
            {
                request_id = requestId,
                approved,
                answered_at = DateTimeOffset.Now,
            }));

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public static class AgentStatusParser
{
    public static AgentSnapshot? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var tool = String(root, "tool") ?? "Agent";
        var state = (String(root, "state") ?? "idle").ToLowerInvariant();

        AgentRequest? request = null;
        if (root.TryGetProperty("request", out var requestElement) && requestElement.ValueKind == JsonValueKind.Object)
        {
            var id = String(requestElement, "id");
            var summary = String(requestElement, "summary");

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(summary))
            {
                request = new AgentRequest(id, summary, String(requestElement, "detail"));
            }
        }

        return new AgentSnapshot(
            tool,
            state,
            Number(root, "used"),
            Number(root, "limit"),
            request,
            DateTimeOffset.TryParse(String(root, "updated_at"), out var updated) ? updated : null);
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
}

// --- Terminal dan otomasi cepat (5c.14) ---

/// <summary>Perintah yang didaftarkan sendiri pengguna; tidak ada sumber luar.</summary>
public sealed record TerminalCommand(string Name, string FileName, string Arguments = "", bool Confirm = true);

public static class TerminalCatalog
{
    public const int MaxCommands = 20;

    public static IReadOnlyList<TerminalCommand> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<TerminalCommand>();

        try
        {
            var loaded = JsonSerializer.Deserialize<List<TerminalCommand>>(json);
            if (loaded is null) return Array.Empty<TerminalCommand>();

            return loaded
                .Where(Valid)
                .Take(MaxCommands)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<TerminalCommand>();
        }
    }

    public static TerminalCommand? Find(IEnumerable<TerminalCommand>? commands, string name)
    {
        if (commands is null || string.IsNullOrWhiteSpace(name)) return null;

        return commands.FirstOrDefault(command =>
            string.Equals(command.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool Valid(TerminalCommand command) =>
        !string.IsNullOrWhiteSpace(command.Name) && !string.IsNullOrWhiteSpace(command.FileName);
}

/// <summary>Daftar perintah lokal di <c>commands.json</c>.</summary>
public sealed class TerminalStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly List<TerminalCommand> items = new();

    public TerminalStore(string? path = null) => FilePath = path ?? DefaultPath;

    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_COMMANDS") is { Length: > 0 } custom
        ? custom
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "commands.json");

    public string FilePath { get; }

    public IReadOnlyList<TerminalCommand> All => items;

    public bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;

            items.Clear();
            items.AddRange(TerminalCatalog.Parse(File.ReadAllText(FilePath)));
            return items.Count > 0;
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

            File.WriteAllText(FilePath, JsonSerializer.Serialize(items, Options));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

// --- Ticker opsional (5c.16) ---

public sealed record TickerItem(string Title, string? Link = null, DateTimeOffset? Published = null);

/// <summary>Pembaca RSS/Atom sederhana; hanya judul dan tautan yang diambil.</summary>
public static class TickerFeed
{
    public const int MaxItems = 10;

    public static TimeSpan RefreshInterval { get; } = TimeSpan.FromMinutes(15);

    public static bool ValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static IReadOnlyList<TickerItem> Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return Array.Empty<TickerItem>();

        try
        {
            var document = XDocument.Parse(xml);
            var nodes = document.Descendants()
                .Where(element => element.Name.LocalName is "item" or "entry");

            var items = new List<TickerItem>();

            foreach (var node in nodes)
            {
                var title = node.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var link = node.Elements().FirstOrDefault(element => element.Name.LocalName == "link")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(link))
                {
                    link = node.Elements().FirstOrDefault(element => element.Name.LocalName == "link")
                        ?.Attribute("href")?.Value;
                }

                var publishedText = node.Elements()
                    .FirstOrDefault(element => element.Name.LocalName is "pubDate" or "updated" or "published")?.Value;

                items.Add(new TickerItem(
                    title,
                    string.IsNullOrWhiteSpace(link) ? null : link,
                    DateTimeOffset.TryParse(publishedText, out var published) ? published : null));

                if (items.Count >= MaxItems) break;
            }

            return items;
        }
        catch (System.Xml.XmlException)
        {
            return Array.Empty<TickerItem>();
        }
    }

    public static bool ShouldRefresh(DateTimeOffset? lastFetch, DateTimeOffset now, bool enabled) =>
        enabled && (lastFetch is null || now - lastFetch >= RefreshInterval);
}

// --- Asisten AI opsional (5c.17) ---

/// <summary>
/// Aturan asisten: hanya endpoint http(s), hanya dikirim saat pengguna menekan
/// kirim, dan kunci disimpan di luar berkas pengaturan oleh App.
/// </summary>
public static class AssistantRules
{
    public static bool ValidEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
         (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "localhost")));

    public static string BuildRequest(string question, string? model = null)
    {
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim(),
            messages = new[]
            {
                new { role = "system", content = "Jawab singkat dalam bahasa pengguna. Jangan mengarang." },
                new { role = "user", content = question.Trim() },
            },
        };

        return JsonSerializer.Serialize(payload);
    }
}

// --- SDK plugin (5c.18) ---

/// <summary>Izin yang boleh diminta plugin. Di luar daftar ini plugin ditolak.</summary>
public static class PluginPermissions
{
    public static IReadOnlySet<string> Allowed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "activity.show",
        "ui.notify",
    };
}

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string EntryPoint,
    IReadOnlyList<string> Permissions);

/// <summary>Kontrak plugin; plugin berjalan di proses ini dengan izin yang dideklarasikan.</summary>
public interface IIslandPlugin
{
    string Id { get; }

    void Start(IPluginContext context);

    void Shutdown();
}

public interface IPluginContext
{
    void Show(string title, string? subtitle = null);
}

public static class PluginCatalog
{
    public const string ManifestName = "plugin.json";

    public static PluginManifest? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var id = Text(root, "id");
            var name = Text(root, "name");
            var version = Text(root, "version") ?? "1.0.0";
            var entry = Text(root, "entry") ?? string.Empty;
            var permissions = new List<string>();

            if (root.TryGetProperty("permissions", out var permissionElement) &&
                permissionElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in permissionElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) permissions.Add(item.GetString()!);
                }
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;

            if (permissions.Any(permission => !PluginPermissions.Allowed.Contains(permission))) return null;

            return new PluginManifest(id, name, version, entry, permissions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Membaca semua manifest valid di folder plugin.</summary>
    public static IReadOnlyList<PluginManifest> Discover(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<PluginManifest>();

        var manifests = new List<PluginManifest>();

        foreach (var file in Directory.EnumerateFiles(folder, ManifestName, SearchOption.AllDirectories))
        {
            try
            {
                var manifest = Parse(File.ReadAllText(file));
                if (manifest is not null) manifests.Add(manifest);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Manifest rusak dilewati; plugin lain tetap jalan.
            }
        }

        return manifests;
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
