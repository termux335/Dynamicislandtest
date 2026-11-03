namespace DynamicIsland.Core;

/// <summary>Jenis aktivitas. Urutan prioritas bawaan ada di <see cref="ActivityKindExtensions.DefaultPriority"/>.</summary>
public enum ActivityKind
{
    Media,
    Timer,
    Call,
    Download,
    Order,
    Notification,
    Battery,
    Device,
    Calendar,
}

/// <summary>Tiga ukuran tampilan kapsul.</summary>
public enum ActivityState
{
    Minimal,
    Compact,
    Expanded,
}

public static class ActivityKindExtensions
{
    /// <summary>Prioritas yang dipakai bila pemanggil tidak memberi nilai eksplisit.</summary>
    public static int DefaultPriority(this ActivityKind kind) => kind switch
    {
        ActivityKind.Call => 100,
        ActivityKind.Timer => 80,
        ActivityKind.Media => 70,
        ActivityKind.Order => 60,
        ActivityKind.Download => 50,
        ActivityKind.Calendar => 40,
        ActivityKind.Notification => 30,
        ActivityKind.Device => 20,
        ActivityKind.Battery => 10,
        _ => 0,
    };

    /// <summary>Live Activity menetap sampai dicabut; Transient Alert hilang sendiri.</summary>
    public static bool IsLiveByDefault(this ActivityKind kind) => kind switch
    {
        ActivityKind.Notification or ActivityKind.Battery or ActivityKind.Device => false,
        _ => true,
    };

    /// <summary>Nama kind seperti yang dipakai di JSON event (<c>shared/events.md</c>).</summary>
    public static string ToWireName(this ActivityKind kind) => kind.ToString().ToLowerInvariant();

    public static bool TryParseWireName(string? name, out ActivityKind kind)
    {
        foreach (var candidate in Enum.GetValues<ActivityKind>())
        {
            if (string.Equals(candidate.ToWireName(), name, StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}

public static class ActivityStateExtensions
{
    /// <summary>Urutan kedalaman tampilan, dipakai untuk transisi bertahap.</summary>
    public static int Depth(this ActivityState state) => state switch
    {
        ActivityState.Minimal => 0,
        ActivityState.Compact => 1,
        _ => 2,
    };

    /// <summary>Satu langkah lebih terbuka, atau null bila sudah maksimum.</summary>
    public static ActivityState? Opened(this ActivityState state) => state switch
    {
        ActivityState.Minimal => ActivityState.Compact,
        ActivityState.Compact => ActivityState.Expanded,
        _ => null,
    };

    /// <summary>Satu langkah lebih tertutup, atau null bila sudah minimum.</summary>
    public static ActivityState? Closed(this ActivityState state) => state switch
    {
        ActivityState.Expanded => ActivityState.Compact,
        ActivityState.Compact => ActivityState.Minimal,
        _ => null,
    };

    public static string ToWireName(this ActivityState state) => state.ToString().ToLowerInvariant();
}

public sealed record ActivityAction(string Id, string Label, string Glyph);

/// <summary>Satu aktivitas. Immutable: perubahan dibuat lewat <c>with</c>.</summary>
public sealed record Activity
{
    public Activity(
        string id,
        ActivityKind kind,
        int? priority = null,
        ActivityState state = ActivityState.Compact,
        string title = "",
        string? subtitle = null,
        double? progress = null,
        IReadOnlyList<ActivityAction>? actions = null,
        bool? isLive = null,
        DateTimeOffset? expiresAt = null,
        TimeSpan? position = null,
        TimeSpan? duration = null,
        string? caption = null)
    {
        Id = id;
        Kind = kind;
        Priority = priority ?? kind.DefaultPriority();
        State = state;
        Title = title;
        Subtitle = subtitle;
        Progress = progress is { } value ? Math.Clamp(value, 0, 1) : null;
        Actions = actions ?? Array.Empty<ActivityAction>();
        IsLive = isLive ?? kind.IsLiveByDefault();
        ExpiresAt = expiresAt;
        Position = position;
        Duration = duration;
        Caption = caption;
    }

    public string Id { get; init; }
    public ActivityKind Kind { get; init; }
    public int Priority { get; init; }
    public ActivityState State { get; init; }
    public string Title { get; init; }
    public string? Subtitle { get; init; }

    /// <summary>Progres 0..1, hanya untuk kind yang punya progres.</summary>
    public double? Progress { get; init; }

    public IReadOnlyList<ActivityAction> Actions { get; init; }
    public bool IsLive { get; init; }

    /// <summary>Hanya bermakna untuk Transient Alert.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Posisi berjalan media, bila sumbernya melaporkan waktu.</summary>
    public TimeSpan? Position { get; init; }

    /// <summary>Durasi total media, bila diketahui.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Baris tambahan bergaya redup di bawah judul. Untuk media dipakai sebagai
    /// baris lirik yang sedang berlaku.
    /// </summary>
    public string? Caption { get; init; }
}
