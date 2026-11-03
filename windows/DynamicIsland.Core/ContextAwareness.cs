namespace DynamicIsland.Core;

/// <summary>
/// Konteks yang dideteksi untuk Mode Tidak Mengganggu (Tahap 4).
///
/// Deteksi nyata ada di lapisan App (status notifikasi Windows dan window
/// depan); tipe ini murni supaya aturan bisa dites tanpa Windows.
/// </summary>
public enum IslandContext
{
    Normal,

    /// <summary>Game layar penuh atau aplikasi Direct3D layar penuh.</summary>
    Game,

    /// <summary>Video layar penuh di peramban (F11).</summary>
    VideoFullscreen,

    /// <summary>Rapat atau panggilan: mikrofon sedang dipakai.</summary>
    Meeting,

    /// <summary>Layar sedang dibagikan atau direkam.</summary>
    ScreenShare,

    /// <summary>Mode presentasi Windows.</summary>
    Presentation,
}

/// <summary>
/// Tingkat respons Mode Tidak Mengganggu, dari paling ramah sampai sembunyi.
/// Urutannya sengaja 1..6 supaya cocok dengan spesifikasi.
/// </summary>
public enum ResponseLevel
{
    /// <summary>Tampil normal.</summary>
    Normal = 1,

    /// <summary>Redup dan ciut jadi kapsul minimal.</summary>
    Dimmed = 2,

    /// <summary>Pindah ke posisi aman, tetap terbaca.</summary>
    Relocate = 3,

    /// <summary>Redup, ciut, dan tembus klik.</summary>
    ClickThrough = 4,

    /// <summary>Hanya peringatan kritis yang lewat.</summary>
    CriticalOnly = 5,

    /// <summary>Sembunyi total.</summary>
    Hidden = 6,
}

/// <summary>
/// Status dari <c>SHQueryUserNotificationState</c> dalam bentuk netral.
/// Angkanya sama dengan enum Win32 supaya pemetaan di App tidak salah.
/// </summary>
public enum UserNotificationState
{
    NotPresent = 1,
    Busy = 2,
    RunningD3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

/// <summary>Efek satu tingkat respons pada kapsul.</summary>
public readonly record struct ResponseEffect(
    bool Visible,
    bool ClickThrough,
    double Opacity,
    ActivityState? ForcedState,
    bool CriticalOnly)
{
    /// <summary>Pemetaan tingkat ke perilaku; dipakai controller dan jendela.</summary>
    public static ResponseEffect For(ResponseLevel level) => level switch
    {
        ResponseLevel.Dimmed => new ResponseEffect(true, false, 0.4, ActivityState.Minimal, false),
        ResponseLevel.Relocate => new ResponseEffect(true, false, 1, null, false),
        ResponseLevel.ClickThrough => new ResponseEffect(true, true, 0.4, ActivityState.Minimal, false),
        ResponseLevel.CriticalOnly => new ResponseEffect(true, true, 0.4, ActivityState.Minimal, true),
        ResponseLevel.Hidden => new ResponseEffect(false, true, 0, ActivityState.Minimal, true),
        _ => new ResponseEffect(true, false, 1, null, false),
    };

    public static ResponseEffect NormalEffect => For(ResponseLevel.Normal);
}

/// <summary>Masukan deteksi konteks dari lapisan Windows.</summary>
public sealed record ContextSignals(
    bool IsScreenSharing = false,
    bool InMeeting = false,
    ForegroundInfo? Foreground = null,
    UserNotificationState Notifications = UserNotificationState.AcceptsNotifications);

/// <summary>Aturan konteks yang murni, tanpa Win32.</summary>
public static class ContextRules
{
    /// <summary>Kunci string untuk isi notifikasi yang disembunyikan saat berbagi layar.</summary>
    public const string HiddenNotificationKey = "notification.hidden";

    /// <summary>
    /// Apakah aktivitas boleh lewat saat tingkat respons "hanya peringatan kritis".
    /// Panggilan, baterai sangat rendah, dan aktivitas yang menandai dirinya kritis.
    /// </summary>
    public static bool IsCritical(Activity activity) =>
        activity.IsCritical || activity.Kind == ActivityKind.Call;

    /// <summary>Isi notifikasi disembunyikan saat layar dibagikan atau direkam, dan saat presentasi.</summary>
    public static bool HidesNotificationContent(IslandContext context) =>
        context is IslandContext.ScreenShare or IslandContext.Presentation;

    /// <summary>
    /// Urutan penentuan konteks. Berbagi layar paling kuat (privasi), lalu
    /// presentasi, rapat, game, dan video layar penuh.
    /// </summary>
    public static IslandContext Classify(ContextSignals signals)
    {
        if (signals.IsScreenSharing) return IslandContext.ScreenShare;
        if (signals.Notifications == UserNotificationState.PresentationMode) return IslandContext.Presentation;
        if (signals.InMeeting) return IslandContext.Meeting;

        if (signals.Foreground is { Category: ForegroundCategory.Game, IsFullscreen: true }) return IslandContext.Game;
        if (signals.Foreground is { Category: ForegroundCategory.Browser, IsFullscreen: true }) return IslandContext.VideoFullscreen;
        if (signals.Notifications == UserNotificationState.RunningD3DFullScreen) return IslandContext.Game;
        if (signals.Notifications == UserNotificationState.Busy) return IslandContext.VideoFullscreen;

        return IslandContext.Normal;
    }
}

/// <summary>Aturan per aplikasi: menimpa tingkat respons dan/atau penempatan.</summary>
public sealed record AppContextRule(
    string Process,
    ResponseLevel? Level = null,
    SafePosition? Position = null,
    double? Opacity = null,
    DisplayMode? Mode = null);

/// <summary>Preferensi Mode Tidak Mengganggu, disimpan bersama pengaturan lain.</summary>
public sealed record ContextAwarenessSettings(
    bool Enabled = true,
    ResponseLevel Game = ResponseLevel.ClickThrough,
    ResponseLevel VideoFullscreen = ResponseLevel.Dimmed,
    ResponseLevel Meeting = ResponseLevel.Dimmed,
    ResponseLevel ScreenShare = ResponseLevel.CriticalOnly,
    ResponseLevel Presentation = ResponseLevel.Dimmed,
    IReadOnlyList<AppContextRule>? Apps = null)
{
    public ResponseLevel LevelFor(IslandContext context) => context switch
    {
        IslandContext.Game => Game,
        IslandContext.VideoFullscreen => VideoFullscreen,
        IslandContext.Meeting => Meeting,
        IslandContext.ScreenShare => ScreenShare,
        IslandContext.Presentation => Presentation,
        _ => ResponseLevel.Normal,
    };

    /// <summary>Aturan aplikasi yang cocok, atau null.</summary>
    public AppContextRule? RuleFor(string? process) =>
        string.IsNullOrWhiteSpace(process)
            ? null
            : Apps?.FirstOrDefault(rule => string.Equals(rule.Process, process, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Hasil akhir konteks: tingkat yang dipakai dan efeknya pada kapsul.</summary>
public sealed record ContextResolution(
    IslandContext Context,
    ResponseLevel Level,
    ResponseEffect Effect,
    string? App = null,
    SafePosition? Position = null,
    DisplayMode? Mode = null)
{
    public static ContextResolution NormalContext { get; } = new(
        IslandContext.Normal,
        ResponseLevel.Normal,
        ResponseEffect.For(ResponseLevel.Normal));
}

public static class ContextResolver
{
    /// <summary>
    /// Menerapkan preferensi dan aturan per aplikasi. Konteks Normal selalu
    /// tampil normal agar mode ini tidak pernah "nyangkut".
    /// </summary>
    public static ContextResolution Resolve(
        IslandContext context,
        string? process = null,
        ContextAwarenessSettings? settings = null)
    {
        settings ??= new ContextAwarenessSettings();

        if (!settings.Enabled || context == IslandContext.Normal) return ContextResolution.NormalContext;

        var rule = settings.RuleFor(process);
        var level = rule?.Level ?? settings.LevelFor(context);
        var effect = ResponseEffect.For(level);

        if (rule?.Opacity is { } opacity) effect = effect with { Opacity = Math.Clamp(opacity, 0, 1) };

        return new ContextResolution(context, level, effect, process, rule?.Position, rule?.Mode);
    }
}

/// <summary>
/// Menahan konteks game kira-kira 3 detik setelah keluar, supaya kapsul tidak
/// berkedip saat Alt-Tab. Masuk ke game selalu langsung.
/// </summary>
public sealed class ContextDetector
{
    private readonly Func<DateTimeOffset> now;
    private DateTimeOffset? releaseAt;

    public ContextDetector(Func<DateTimeOffset>? now = null, TimeSpan? releaseDelay = null)
    {
        this.now = now ?? (() => DateTimeOffset.Now);
        ReleaseDelay = releaseDelay ?? TimeSpan.FromSeconds(3);
    }

    public TimeSpan ReleaseDelay { get; }

    public IslandContext Current { get; private set; } = IslandContext.Normal;

    public event Action<IslandContext>? Changed;

    /// <summary>Mengamati konteks baru; mengembalikan konteks yang berlaku sekarang.</summary>
    public IslandContext Observe(IslandContext detected)
    {
        var at = now();

        if (detected == Current)
        {
            releaseAt = null;
            return Current;
        }

        if (Current == IslandContext.Game)
        {
            releaseAt ??= at + ReleaseDelay;
            if (at < releaseAt) return Current;
        }

        if (detected == IslandContext.Game) releaseAt = null;

        Current = detected;
        Changed?.Invoke(detected);
        return Current;
    }

    /// <summary>Mengembalikan ke Normal tanpa menunggu (mis. saat fitur dimatikan).</summary>
    public void Reset()
    {
        releaseAt = null;
        if (Current == IslandContext.Normal) return;

        Current = IslandContext.Normal;
        Changed?.Invoke(Current);
    }
}
