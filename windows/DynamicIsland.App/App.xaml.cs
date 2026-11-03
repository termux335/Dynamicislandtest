using System.Windows;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

public partial class App : Application, IDisposable
{
    private bool disposed;

    private IslandController? controller;
    private IslandEventPipe? pipe;
    private DispatcherTimer? ticker;
    private BatteryMonitor? batteryMonitor;
    private DriveMonitor? driveMonitor;
    private CalendarMonitor? calendarMonitor;
    private ForegroundWatcher? foregroundWatcher;
    private TimerMonitor? timerMonitor;
    private MediaMonitor? mediaMonitor;
    private MicrophoneMonitor? microphoneMonitor;
    private NotificationMonitor? notificationMonitor;
    private IslandViewModel? model;
    private SettingsWindow? settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        controller = new IslandController();
        controller.OnError = message => Log.Write("event diabaikan: " + message);

        // Preferensi dari sesi sebelumnya (mode, dua kapsul, penempatan, bahasa,
        // kepribadian, aksen, posisi, prioritas).
        var settings = SettingsStore.Load();
        var firstRun = !SettingsStore.Exists();
        controller.DisplayMode = settings.DisplayMode;
        controller.ShowsSecondary = settings.ShowsSecondary;
        controller.PriorityPreset = settings.Priority;

        var text = new IslandText(settings.Language, settings.Humor);
        Theme.Apply(settings.Accent);

        var model = new IslandViewModel(controller, text, settings.Accent, settings.Position)
        {
            AutoPlacement = settings.AutoPlacement,
        };
        this.model = model;

        var timer = new TimerMonitor(controller, Dispatcher, text);
        var media = new MediaMonitor(controller, Dispatcher, text);
        timerMonitor = timer;
        mediaMonitor = media;

        var notifications = new NotificationMonitor(controller, Dispatcher, text);
        notificationMonitor = notifications;

        var window = new IslandWindow(model, controller, timer, media, notifications);
        window.SettingsChanged += SaveSettings;
        var calendar = new CalendarMonitor(controller, Dispatcher, text);
        calendarMonitor = calendar;

        window.ActionInvoked += action =>
        {
            Log.Write($"aksi ditekan: {action.Id} ({action.Label})");

            switch (action.Id)
            {
                case "join": calendar.OpenMeeting(); break;
                case "media-toggle": media.TogglePlayPause(); break;
                case "media-next": media.Next(); break;
                case "media-previous": media.Previous(); break;
                case "timer-toggle": timer.ToggleTimer(); break;
                case "timer-reset": timer.ResetTimer(); break;
                case "meeting.mic-toggle": microphoneMonitor?.ToggleMute(); break;
                case "meeting.back": microphoneMonitor?.BackToMeeting(); break;
                case "notification.dismiss": notifications.DismissLatest(); break;
            }
        };

        MainWindow = window;
        window.Show();

        // Uji manual DPI/multi-monitor: angka DPI nyata per monitor dan ukuran
        // kapsul dalam DIP dicatat sekali saat mulai.
        DisplayInfo.LogMonitors();
        var scale = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        Log.Write(
            $"kapsul: {model.Size.Width}x{model.Size.Height} DIP di ({window.Left:0},{window.Top:0}) " +
            $"pada skala {scale.DpiScaleX:0.##}x (" + $"{scale.PixelsPerDip * 96:0} dpi)");

        window.SettingsRequested += () => OpenSettings(firstRun: false);

        // Autostart dibaca dari registry, bukan dari berkas: itu sumber sebenarnya.
        model.StartWithWindows = StartupRegistration.IsEnabled();

        // Onboarding hanya saat belum ada berkas pengaturan, dan tidak saat demo
        // atau uji manual (yang tidak boleh terhalang jendela).
        var demo = e.Args.Contains("--demo") || Environment.GetEnvironmentVariable("ISLAND_DEMO") == "1";
        if (firstRun && !demo && Environment.GetEnvironmentVariable("ISLAND_NO_ONBOARDING") != "1")
        {
            Log.Write("onboarding: jalan pertama, membuka pengaturan awal");
            Dispatcher.BeginInvoke(() => OpenSettings(firstRun: true), DispatcherPriority.Background);
        }

        // Uji manual autostart: daftar, baca, cabut, lalu catat hasilnya.
        if (Environment.GetEnvironmentVariable("ISLAND_AUTOSTART_CHECK") == "1")
        {
            StartupRegistration.SelfCheck();
        }

        pipe = new IslandEventPipe(
            line => Dispatcher.BeginInvoke(() => controller.HandleLine(line)),
            Environment.GetEnvironmentVariable("ISLAND_PIPE"));
        pipe.Start();
        Log.Write($"Dynamic Island {AppInfo.Version} jalan; event lewat named pipe {pipe.PipeName}");

        ticker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Background,
            (_, _) => controller.Tick(),
            Dispatcher);
        ticker.Start();

        // Tahap 2: monitor lokal tanpa izin tambahan.
        calendar.Start();

        batteryMonitor = new BatteryMonitor(controller, Dispatcher, text);
        batteryMonitor.Start();

        driveMonitor = new DriveMonitor(controller, Dispatcher, text);
        driveMonitor.Start();

        // Tahap 3: timer asli dan Now Playing sungguhan.
        timer.Start();
        media.Start();

        // Tahap 1 (sisa): rapat lewat status mikrofon, dan notifikasi Windows
        // bila pengguna menyalakannya (izin diminta saat itu juga).
        microphoneMonitor = new MicrophoneMonitor(controller, Dispatcher, text);

        try
        {
            microphoneMonitor.Start();
        }
        catch (Exception error)
        {
            // Perangkat audio bermasalah tidak boleh menjatuhkan island.
            Log.Write("mic: monitor gagal dimulai (" + error + ")");
        }

        model.NotificationsEnabled = settings.NotificationsEnabled;
        if (settings.NotificationsEnabled) notifications.SetEnabled(true);

        // Jalan pintas uji manual, sama seperti ISLAND_SECONDARY.
        if (Environment.GetEnvironmentVariable("ISLAND_NOTIFICATIONS") == "1")
        {
            model.NotificationsEnabled = true;
            notifications.SetEnabled(true);
        }

        // Tahap 3: dua kapsul bisa dinyalakan dari menu klik kanan, atau lewat
        // variabel ini untuk uji manual tanpa menyentuh menu.
        if (Environment.GetEnvironmentVariable("ISLAND_SECONDARY") == "1") controller.ShowsSecondary = true;

        // Tahap 3: penempatan adaptif untuk game layar penuh dan peramban maximized.
        foregroundWatcher = new ForegroundWatcher(Dispatcher);
        foregroundWatcher.Changed += info =>
        {
            model.SetForeground(info);
            Log.Write(info is null
                ? "window depan tidak terbaca; kapsul di tengah atas"
                : $"window depan: {info.Category}, layar penuh {info.IsFullscreen}, maximized {info.IsMaximized}");
        };
        foregroundWatcher.Start();

        if (demo)
        {
            DemoEvents.Seed(controller);
        }
    }

    /// <summary>
    /// Membuka onboarding (jalan pertama) atau pengaturan. Perubahan langsung
    /// berlaku karena jendela memakai <see cref="IslandViewModel"/> yang sama.
    /// </summary>
    private void OpenSettings(bool firstRun)
    {
        if (model is null) return;

        if (settingsWindow is { IsVisible: true })
        {
            settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow(model, firstRun)
        {
            ApplyAutostart = StartupRegistration.Set,
        };

        window.Closed += (_, _) =>
        {
            settingsWindow = null;
            // Menutup lewat tombol X tetap dianggap selesai, supaya onboarding
            // tidak muncul lagi setiap kali app dijalankan.
            SaveSettings();
        };

        settingsWindow = window;
        window.Show();
    }

    /// <summary>Menyimpan mode, dua kapsul, dan penempatan setelah diubah dari menu.</summary>
    private void SaveSettings()
    {
        if (controller is null || model is null) return;

        var settings = new IslandSettings(
            controller.DisplayMode,
            controller.ShowsSecondary,
            model.AutoPlacement,
            model.Language,
            model.Humor,
            model.Accent,
            model.Position,
            controller.PriorityPreset,
            model.NotificationsEnabled,
            model.StartWithWindows);

        if (!SettingsStore.Save(settings)) Log.Write("pengaturan: gagal disimpan");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    /// <summary>Menutup semua monitor dan pipe. Dipanggil sekali dari OnExit.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        ticker?.Stop();
        batteryMonitor?.Dispose();
        driveMonitor?.Dispose();
        calendarMonitor?.Dispose();
        foregroundWatcher?.Dispose();
        timerMonitor?.Dispose();
        mediaMonitor?.Dispose();
        microphoneMonitor?.Dispose();
        notificationMonitor?.Dispose();
        pipe?.Dispose();

        GC.SuppressFinalize(this);
    }
}
