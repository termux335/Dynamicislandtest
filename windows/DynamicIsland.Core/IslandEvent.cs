using System.Text.Json;

namespace DynamicIsland.Core;

/// <summary>Satu event palsu dari <c>shared/events.md</c>.</summary>
public abstract record IslandEvent
{
    /// <summary>Umur bawaan Transient Alert, dalam milidetik.</summary>
    public const int DefaultTtlMs = 4000;

    public sealed record Upsert(Activity Activity) : IslandEvent;

    public sealed record Progress(string Id, double Value) : IslandEvent;

    public sealed record Expire(string Id) : IslandEvent;

    public sealed record Clear(string Id) : IslandEvent;

    public sealed record ClearAll : IslandEvent;

    /// <summary>Membaca satu baris JSON.</summary>
    /// <exception cref="IslandEventException">Baris tidak valid.</exception>
    public static IslandEvent Parse(string line, DateTimeOffset now)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException error)
        {
            throw new IslandEventException("JSON tidak valid: " + error.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new IslandEventException("JSON tidak valid: root harus objek");
            }

            if (!root.TryGetProperty("op", out var opElement) || opElement.ValueKind != JsonValueKind.String)
            {
                throw new IslandEventException("field wajib tidak ada: op");
            }

            var op = opElement.GetString()!;
            return op switch
            {
                "upsert" => new Upsert(ParseActivity(RequireObject(root, "activity"), now)),
                "progress" => new Progress(RequireString(root, "id"), RequireNumber(root, "progress")),
                "expire" => new Expire(RequireString(root, "id")),
                "clear" => new Clear(RequireString(root, "id")),
                "clearAll" => new ClearAll(),
                _ => throw new IslandEventException("op tidak dikenal: " + op),
            };
        }
    }

    private static Activity ParseActivity(JsonElement payload, DateTimeOffset now)
    {
        var id = RequireString(payload, "id");

        var kindName = payload.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String
            ? kindElement.GetString()
            : ActivityKind.Notification.ToWireName();

        if (!ActivityKindExtensions.TryParseWireName(kindName, out var kind))
        {
            throw new IslandEventException("kind tidak dikenal: " + kindName);
        }

        var isLive = payload.TryGetProperty("isLive", out var liveElement) && liveElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? liveElement.GetBoolean()
            : kind.IsLiveByDefault();

        var ttlMs = payload.TryGetProperty("ttlMs", out var ttlElement) && ttlElement.ValueKind == JsonValueKind.Number
            ? ttlElement.GetDouble()
            : DefaultTtlMs;

        var actions = new List<ActivityAction>();
        if (payload.TryGetProperty("actions", out var actionsElement) && actionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in actionsElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (!entry.TryGetProperty("id", out var entryId) || entryId.ValueKind != JsonValueKind.String) continue;

                var actionId = entryId.GetString()!;
                actions.Add(new ActivityAction(
                    actionId,
                    OptionalString(entry, "label") ?? actionId,
                    OptionalString(entry, "glyph") ?? "circle"));
            }
        }

        return new Activity(
            id: id,
            kind: kind,
            priority: OptionalInt(payload, "priority"),
            state: StateMachine.InitialState,
            title: OptionalString(payload, "title") ?? string.Empty,
            subtitle: OptionalString(payload, "subtitle"),
            progress: payload.TryGetProperty("progress", out var progressElement) && progressElement.ValueKind == JsonValueKind.Number
                ? progressElement.GetDouble()
                : null,
            actions: actions,
            isLive: isLive,
            expiresAt: isLive ? null : now.AddMilliseconds(ttlMs));
    }

    private static JsonElement RequireObject(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new IslandEventException("field wajib tidak ada: " + key);
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new IslandEventException("field wajib tidak ada: " + key);
        }

        return value.GetString()!;
    }

    private static double RequireNumber(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new IslandEventException("field wajib tidak ada: " + key);
        }

        return value.GetDouble();
    }

    private static string? OptionalString(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? OptionalInt(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
}

public sealed class IslandEventException : Exception
{
    public IslandEventException(string message) : base(message)
    {
    }
}
