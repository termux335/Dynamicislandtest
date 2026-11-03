using System.Runtime.InteropServices;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Mendeteksi konteks Mode Tidak Mengganggu (Tahap 4).
///
/// Sumbernya hanya yang sudah dipakai app ini: status notifikasi Windows
/// (<c>SHQueryUserNotificationState</c>), window depan dari
/// <see cref="ForegroundWatcher"/>, dan pemakai mikrofon dari
/// <see cref="MicrophoneMonitor"/>. Tidak ada hook, injeksi, atau pembacaan
/// memori proses lain.
///
/// Untuk uji manual ada jalur event palsu: <c>ISLAND_CONTEXT=game|video|meeting|share|presentation|normal</c>
/// dan <c>ISLAND_SCREEN_SHARE=1</c>.
/// </summary>
internal sealed class ContextMonitor : IDisposable
{
    public const string ContextVariable = "ISLAND_CONTEXT";
    public const string ShareVariable = "ISLAND_SCREEN_SHARE";

    private readonly IslandController controller;
    private readonly DispatcherTimer timer;
    private readonly ContextDetector detector = new();
    private ForegroundWatcher? foreground;
    private MicrophoneMonitor? microphone;
    private ContextResolution lastResolution = ContextResolution.NormalContext;

    public ContextMonitor(IslandController controller, Dispatcher dispatcher)
    {
        this.controller = controller;
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    public ContextAwarenessSettings Settings { get; set; } = new();

    public bool Enabled { get; set; } = true;

    /// <summary>Saklar manual untuk berbagi layar, karena Windows tidak selalu melaporkannya.</summary>
    public bool ForceShare { get; set; }

    /// <summary>Profil per aplikasi (posisi terakhir per game).</summary>
    public AppProfileStore? Profiles { get; set; }

    public IslandContext Current => detector.Current;

    public ContextResolution LastResolution => lastResolution;

    public event Action<ContextResolution>? ResolutionChanged;

    public void Attach(ForegroundWatcher watcher, MicrophoneMonitor mic)
    {
        foreground = watcher;
        microphone = mic;
    }

    public void Start()
    {
        Poll();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    public void SetForceShare(bool sharing)
    {
        if (ForceShare == sharing) return;

        ForceShare = sharing;
        Poll();
    }

    /// <summary>Dipanggil berkala; publik supaya uji manual bisa memaksa satu putaran.</summary>
    public void Poll()
    {
        if (!Enabled)
        {
            detector.Reset();
            controller.ClearContext();
            Publish(ContextResolution.NormalContext);
            return;
        }

        detector.Observe(Detect());

        var process = foreground?.Current?.ProcessName;
        var resolution = ContextResolver.Resolve(detector.Current, process, Settings);
        var profile = Profiles?.For(process);

        if (profile is not null)
        {
            resolution = resolution with
            {
                Position = resolution.Position ?? profile.Position,
                Mode = resolution.Mode ?? profile.Mode,
            };
        }

        controller.SetContext(resolution);
        Publish(resolution);

        if (detector.Current != IslandContext.Normal && !string.IsNullOrWhiteSpace(process))
        {
            Profiles?.Remember(new AppProfile(
                process,
                resolution.Position ?? profile?.Position ?? SafePosition.TopCenter,
                resolution.Mode ?? profile?.Mode ?? DisplayMode.Standar,
                resolution.Effect.Opacity,
                resolution.Level,
                DateTimeOffset.Now));
            Profiles?.Save();
        }
    }

    private void Publish(ContextResolution resolution)
    {
        if (resolution == lastResolution) return;

        lastResolution = resolution;
        ResolutionChanged?.Invoke(resolution);
    }

    private IslandContext Detect()
    {
        if (TryParseContext(Environment.GetEnvironmentVariable(ContextVariable), out var forced)) return forced;

        var sharing = ForceShare ||
            string.Equals(Environment.GetEnvironmentVariable(ShareVariable), "1", StringComparison.Ordinal);

        var signals = new ContextSignals(
            IsScreenSharing: sharing,
            InMeeting: microphone?.CurrentApp is not null,
            Foreground: foreground?.Current,
            Notifications: ReadNotificationState());

        return ContextRules.Classify(signals);
    }

    /// <summary>Membaca status Win32. Gagal dibaca berarti "menerima notifikasi".</summary>
    internal static UserNotificationState ReadNotificationState()
    {
        try
        {
            return SHQueryUserNotificationState(out var state) == 0 && Enum.IsDefined(typeof(UserNotificationState), state)
                ? (UserNotificationState)state
                : UserNotificationState.AcceptsNotifications;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return UserNotificationState.AcceptsNotifications;
        }
    }

    public static bool TryParseContext(string? value, out IslandContext context)
    {
        context = value?.Trim().ToLowerInvariant() switch
        {
            "game" => IslandContext.Game,
            "video" => IslandContext.VideoFullscreen,
            "meeting" => IslandContext.Meeting,
            "share" => IslandContext.ScreenShare,
            "presentation" => IslandContext.Presentation,
            "normal" => IslandContext.Normal,
            _ => (IslandContext)(-1),
        };

        return (int)context >= 0;
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
