using System.Runtime.InteropServices;
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
    private DispatcherTimer? oledTimer;
    private BatteryMonitor? batteryMonitor;
    private DriveMonitor? driveMonitor;
    private CalendarMonitor? calendarMonitor;
    private ForegroundWatcher? foregroundWatcher;
    private TimerMonitor? timerMonitor;
    private MediaMonitor? mediaMonitor;
    private MicrophoneMonitor? microphoneMonitor;
    private NotificationMonitor? notificationMonitor;
    private ContextMonitor? contextMonitor;
    private PrayerMonitor? prayerMonitor;
    private WeatherMonitor? weatherMonitor;
    private HabitMonitor? habitMonitor;
    private ScenarioMonitor? scenarioMonitor;
    private IdleWatcher? idleWatcher;
    private ClipboardHistory? clipboardHistory;
    private ClipboardMonitor? clipboardMonitor;
    private OsdMonitor? osdMonitor;
    private ShortcutStore? shortcutStore;
    private PaletteWindow? paletteWindow;
    private CameraMirrorWindow? cameraWindow;
    private IslandViewModel? model;
    private IslandWindow? window;
    private SettingsWindow? settingsWindow;

    private ContextAwarenessSettings contextSettings = new();
    private string? weatherAdm4;
    private OledPhase lastOledPhase = OledPhase.Active;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        controller = new IslandController();
        controller.OnError = message => Log.Write("event diabaikan: " + message);

        // Preferensi dari sesi sebelumnya.
        var settings = SettingsStore.Load();
        var firstRun = !SettingsStore.Exists();
        controller.DisplayMode = settings.DisplayMode;
        controller.ShowsSecondary = settings.ShowsSecondary;
        controller.PriorityPreset = settings.Priority;

        contextSettings = settings.Context ?? new ContextAwarenessSettings();
        weatherAdm4 = settings.WeatherAdm4;

        var text = new IslandText(settings.Language, settings.Humor);
        Theme.Apply(settings.Accent);

        var model = new IslandViewModel(controller, text, settings.Accent, settings.Position)
        {
            AutoPlacement = settings.AutoPlacement,
            ContextEnabled = contextSettings.Enabled,
            SafeShare = settings.SafeShare,
            OledProtection = settings.OledProtection,
            PrayerEnabled = settings.PrayerEnabled,
            HabitsEnabled = settings.HabitsEnabled,
            StudyMode = settings.StudyMode,
            WeatherEnabled = settings.WeatherEnabled,
            ClipboardEnabled = settings.ClipboardEnabled,
            PaletteEnabled = settings.PaletteEnabled,
            OsdEnabled = settings.OsdEnabled,
            MirrorEnabled = settings.MirrorEnabled,
        };
        this.model = model;

        var timer = new TimerMonitor(controller, Dispatcher, text);
        var media = new MediaMonitor(controller, Dispatcher, text);
        timerMonitor = timer;
        mediaMonitor = media;

        var notifications = new NotificationMonitor(controller, Dispatcher, text);
        notificationMonitor = notifications;

        var window = new IslandWindow(model, controller, timer, media, notifications);
        this.window = window;
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
                default:
                    if (action.Id.StartsWith("shortcut:", StringComparison.Ordinal))
                    {
                        OpenShortcut(action.Id["shortcut:".Length..]);
                    }
                    break;
            }
        };

        MainWindow = window;
        window.Show();

        DisplayInfo.LogMonitors();
        var scale = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        Log.Write(
            $"kapsul: {model.Size.Width}x{model.Size.Height} DIP di ({window.Left:0},{window.Top:0}) " +
            $"pada skala {scale.DpiScaleX:0.##}x ({scale.PixelsPerDip * 96:0} dpi)");

        window.SettingsRequested += () => OpenSettings(firstRun: false);

        model.StartWithWindows = StartupRegistration.IsEnabled();

        var demo = e.Args.Contains("--demo") || Environment.GetEnvironmentVariable("ISLAND_DEMO") == "1";
        if (firstRun && !demo && Environment.GetEnvironmentVariable("ISLAND_NO_ONBOARDING") != "1")
        {
            Log.Write("onboarding: jalan pertama, membuka pengaturan awal");
            Dispatcher.BeginInvoke(() => OpenSettings(firstRun: true), DispatcherPriority.Background);
        }

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

        // Tahap 1 (sisa): rapat lewat status mikrofon, dan notifikasi Windows.
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

        if (Environment.GetEnvironmentVariable("ISLAND_NOTIFICATIONS") == "1")
        {
            model.NotificationsEnabled = true;
            notifications.SetEnabled(true);
        }

        if (Environment.GetEnvironmentVariable("ISLAND_SECONDARY") == "1") controller.ShowsSecondary = true;

        foregroundWatcher = new ForegroundWatcher(Dispatcher);
        foregroundWatcher.Changed += info =>
        {
            model.SetForeground(info);
            Log.Write(info is null
                ? "window depan tidak terbaca; kapsul di tengah atas"
                : $"window depan: {info.Category}, layar penuh {info.IsFullscreen}, maximized {info.IsMaximized}, proses {info.ProcessName}");
        };
        foregroundWatcher.Start();

        // Tahap 4: Mode Tidak Mengganggu dan modul-modulnya.
        var profiles = new AppProfileStore();
        if (profiles.Load()) Log.Write($"profil aplikasi: {profiles.All.Count} dimuat");

        contextMonitor = new ContextMonitor(controller, Dispatcher)
        {
            Settings = contextSettings,
            Enabled = model.ContextEnabled,
            Profiles = profiles,
        };
        contextMonitor.Attach(foregroundWatcher, microphoneMonitor);
        contextMonitor.ResolutionChanged += OnResolutionChanged;
        contextMonitor.Start();

        scenarioMonitor = new ScenarioMonitor(controller, Dispatcher, text, () => contextMonitor?.Current ?? IslandContext.Normal)
        {
            StudyMode = () => model.StudyMode,
        };
        scenarioMonitor.PlanChanged += plan => notifications.Suppress = plan.SuppressNotifications;
        scenarioMonitor.Start();

        prayerMonitor = new PrayerMonitor(controller, Dispatcher, text);
        prayerMonitor.SetEnabled(model.PrayerEnabled);

        weatherMonitor = new WeatherMonitor(controller, Dispatcher, text);
        weatherMonitor.Configure(model.WeatherEnabled, weatherAdm4);

        habitMonitor = new HabitMonitor(controller, Dispatcher, text);
        habitMonitor.Configure(model.HabitsEnabled);
        habitMonitor.Start();

        idleWatcher = new IdleWatcher(Dispatcher);
        idleWatcher.Start();

        oledTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(5),
            DispatcherPriority.Background,
            (_, _) => ApplyOled(),
            Dispatcher);
        oledTimer.Start();

        window.HotkeyPressed += key => HandleHotkey(key, media, microphoneMonitor);

        // Tahap 5a: clipboard, palette, OSD, dan pintasan aplikasi.
        clipboardHistory = new ClipboardHistory();
        clipboardMonitor = new ClipboardMonitor(clipboardHistory, Dispatcher, text);
        clipboardMonitor.SetEnabled(model.ClipboardEnabled);

        shortcutStore = new ShortcutStore();
        if (shortcutStore.Load()) Log.Write($"pintasan: {shortcutStore.All.Count} dimuat");
        model.Shortcuts = shortcutStore.All;

        osdMonitor = new OsdMonitor(controller, Dispatcher, text);
        osdMonitor.SetEnabled(model.OsdEnabled);

        // Jalan pintas uji manual modul Tahap 5a.
        if (Environment.GetEnvironmentVariable("ISLAND_CLIPBOARD") == "1")
        {
            model.ClipboardEnabled = true;
            clipboardMonitor.SetEnabled(true);
        }

        if (Environment.GetEnvironmentVariable("ISLAND_OSD") == "1")
        {
            model.OsdEnabled = true;
            osdMonitor.SetEnabled(true);
        }

        if (Environment.GetEnvironmentVariable("ISLAND_MIRROR") == "1") model.MirrorEnabled = true;

        window.PaletteRequested += TogglePalette;
        window.ClipboardRequested += TogglePalette;
        window.ClipboardClearRequested += ClearClipboard;
        window.CameraRequested += ToggleCamera;
        window.ShortcutsRequested += OpenShortcuts;

        // Kamera tidak boleh hidup saat island menciut atau disembunyikan.
        controller.Changed += presentation =>
        {
            if (cameraWindow is null) return;
            if (!presentation.Visible || presentation.Primary is { State: not ActivityState.Expanded })
            {
                cameraWindow.Close();
            }
        };

        if (demo) DemoEvents.Seed(controller);
    }

    /// <summary>Konteks berubah: perbarui posisi aman, privasi, dan catatan log.</summary>
    private void OnResolutionChanged(ContextResolution resolution)
    {
        if (model is null) return;

        model.SafePosition = resolution.Position;
        UpdatePrivacy();

        var suffix = string.IsNullOrEmpty(resolution.App) ? string.Empty : $" ({resolution.App})";
        Log.Write($"konteks: {resolution.Context} → tingkat {resolution.Level}{suffix}");
    }

    /// <summary>Isi notifikasi hanya disembunyikan bila pengguna mengizinkan dan layar sedang dibagikan.</summary>
    private void UpdatePrivacy()
    {
        if (notificationMonitor is null || model is null) return;

        var context = contextMonitor?.Current ?? IslandContext.Normal;
        notificationMonitor.PrivacyHidden = model.SafeShare && ContextRules.HidesNotificationContent(context);
    }

    /// <summary>Menerapkan perubahan saklar modul tanpa restart.</summary>
    private void ApplyModules()
    {
        if (model is null) return;

        contextSettings = contextSettings with { Enabled = model.ContextEnabled };

        if (contextMonitor is not null)
        {
            contextMonitor.Settings = contextSettings;
            contextMonitor.Enabled = model.ContextEnabled;
            contextMonitor.Poll();
        }

        prayerMonitor?.SetEnabled(model.PrayerEnabled);
        weatherMonitor?.Configure(model.WeatherEnabled, weatherAdm4);
        habitMonitor?.Configure(model.HabitsEnabled);
        clipboardMonitor?.SetEnabled(model.ClipboardEnabled);
        osdMonitor?.SetEnabled(model.OsdEnabled);
        scenarioMonitor?.Poll();

        if (notificationMonitor is not null && scenarioMonitor is not null)
        {
            notificationMonitor.Suppress = scenarioMonitor.Plan.SuppressNotifications;
        }

        UpdatePrivacy();
    }

    /// <summary>Pelindung layar OLED: geser mikro, redup, lalu sembunyi saat idle.</summary>
    private void ApplyOled()
    {
        if (window is null || idleWatcher is null || model is null) return;

        var state = OledGuard.StateFor(idleWatcher.Idle, idleWatcher.Uptime, model.OledProtection);
        window.ApplyOled(state);

        if (state.Phase != lastOledPhase)
        {
            Log.Write($"oled: {state.Phase} setelah idle {idleWatcher.Idle.TotalSeconds:0} detik");
            lastOledPhase = state.Phase;
        }
    }

    /// <summary>Pintasan global: kontrol tanpa keluar dari game, dengan umpan balik singkat.</summary>
    private void HandleHotkey(IslandHotkey key, MediaMonitor media, MicrophoneMonitor? microphone)
    {
        switch (key)
        {
            case IslandHotkey.PlayPause:
                var wasPlaying = model?.MediaPlaying == true;
                media.TogglePlayPause();
                Feedback(wasPlaying ? "hotkey.paused" : "hotkey.playing");
                break;

            case IslandHotkey.Next:
                media.Next();
                Feedback("hotkey.next");
                break;

            case IslandHotkey.MuteMic:
                microphone?.ToggleMute();
                Feedback(microphone?.IsMuted == true ? "hotkey.muted" : "hotkey.unmuted");
                break;

            case IslandHotkey.Palette:
                TogglePalette();
                break;
        }
    }

    // --- Tahap 5a: palette, clipboard, kamera, pintasan ---

    private void TogglePalette()
    {
        if (model is null) return;
        if (!model.PaletteEnabled)
        {
            Log.Write("palette: dimatikan di pengaturan");
            return;
        }

        if (paletteWindow is { IsVisible: true })
        {
            paletteWindow.Close();
            return;
        }

        var window = new PaletteWindow(
            model.Text.Get("palette.title"),
            model.Text.Get("palette.placeholder"),
            model.Text.Get("palette.hint"),
            model.Text.Get("palette.empty"));

        window.Search = BuildPalette;
        window.Chosen += ExecutePalette;
        window.Closed += (_, _) => paletteWindow = null;

        paletteWindow = window;
        window.Show();
        window.Activate();
    }

    private IReadOnlyList<PaletteEntry> BuildPalette(string query)
    {
        if (model is null) return Array.Empty<PaletteEntry>();

        var text = model.Text;
        var labels = new PaletteLabels(
            text.Get("palette.copy"),
            "Folder",
            "Aplikasi",
            text.Get("clipboard.title"));

        clipboardHistory?.Purge();

        var entries = Palette.Build(query, shortcutStore?.All, clipboardHistory?.Items, labels).ToList();

        var commands = new[]
        {
            (Id: "media-toggle", Label: text.Get("quickAction.mediaToggle")),
            (Id: "media-next", Label: text.Get("quickAction.mediaNext")),
            (Id: "timer-focus", Label: text.Get("quickAction.timerFocus")),
            (Id: "clipboard-clear", Label: text.Get("menu.clipboardClear")),
            (Id: "camera", Label: text.Get("menu.camera")),
        };

        var trimmed = query.Trim();

        foreach (var command in commands)
        {
            if (trimmed.Length > 0 && !command.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) continue;

            entries.Add(new PaletteEntry(command.Id, command.Label, text.Get("menu.palette"), PaletteKind.Command, string.Empty));
        }

        return entries.Take(8).ToList();
    }

    private void ExecutePalette(PaletteEntry entry)
    {
        switch (entry.Kind)
        {
            case PaletteKind.Calculation:
            case PaletteKind.Clipboard:
                try
                {
                    System.Windows.Clipboard.SetText(entry.Payload);
                    Log.Write("palette: hasil disalin ke clipboard");
                }
                catch (ExternalException error)
                {
                    Log.Write("palette: gagal menyalin (" + error.Message + ")");
                }
                break;

            case PaletteKind.Shortcut:
                StartPath(entry.Payload);
                break;

            default:
                switch (entry.Id)
                {
                    case "media-toggle": mediaMonitor?.TogglePlayPause(); break;
                    case "media-next": mediaMonitor?.Next(); break;
                    case "timer-focus": timerMonitor?.StartFocus(); break;
                    case "clipboard-clear": ClearClipboard(); break;
                    case "camera": ToggleCamera(); break;
                }
                break;
        }
    }

    private void ClearClipboard()
    {
        clipboardHistory?.Clear();
        Log.Write("clipboard: riwayat dihapus pengguna");
        Notify("clipboard.title", "clipboard.cleared");
    }

    private void ToggleCamera()
    {
        if (model is null) return;

        if (!model.MirrorEnabled)
        {
            Log.Write("kamera: modul dimatikan di pengaturan");
            return;
        }

        if (cameraWindow is { IsVisible: true })
        {
            cameraWindow.Close();
            return;
        }

        var window = new CameraMirrorWindow(
            model.Text,
            model.Text.Get("camera.title"),
            model.Text.Get("camera.active"),
            model.Text.Get("camera.denied"),
            model.Text.Get("camera.close"));

        window.Closed += (_, _) => cameraWindow = null;
        cameraWindow = window;
        window.Show();
    }

    private void OpenShortcuts()
    {
        if (shortcutStore is null) return;

        shortcutStore.Save();
        StartPath(shortcutStore.FilePath);
    }

    private void OpenShortcut(string id)
    {
        var shortcut = shortcutStore?.All.FirstOrDefault(item => item.Id == id);
        if (shortcut is null)
        {
            Log.Write($"pintasan: {id} tidak ditemukan");
            return;
        }

        StartPath(shortcut.Path);
    }

    private static void StartPath(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            Log.Write($"pintasan: membuka {path}");
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.FileNotFoundException)
        {
            Log.Write($"pintasan: gagal membuka {path}: {error.Message}");
        }
    }

    private void Notify(string titleKey, string messageKey)
    {
        if (controller is null || model is null) return;

        controller.Refresh(new Activity(
            id: "module-feedback",
            kind: ActivityKind.Notification,
            priority: 92,
            title: model.Text.Get(titleKey),
            subtitle: model.Text.Get(messageKey),
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddMilliseconds(1600)));
    }

    private void Feedback(string key)
    {
        if (controller is null || model is null) return;

        controller.Refresh(new Activity(
            id: "hotkey-feedback",
            kind: ActivityKind.Notification,
            priority: 96,
            title: model.Text.Get("hotkey.title"),
            subtitle: model.Text.Get(key),
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddMilliseconds(1600)));
    }

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
            // Menutup lewat tombol X tetap dianggap selesai.
            SaveSettings();
        };

        settingsWindow = window;
        window.Show();
    }

    /// <summary>Menyimpan mode, konteks, dan saklar modul Tahap 4.</summary>
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
            model.StartWithWindows,
            model.SafeShare,
            model.OledProtection,
            model.PrayerEnabled,
            model.WeatherEnabled,
            weatherAdm4,
            model.HabitsEnabled,
            model.StudyMode,
            model.ClipboardEnabled,
            model.PaletteEnabled,
            model.OsdEnabled,
            model.MirrorEnabled,
            contextSettings with { Enabled = model.ContextEnabled });

        if (!SettingsStore.Save(settings)) Log.Write("pengaturan: gagal disimpan");

        ApplyModules();
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
        oledTimer?.Stop();
        batteryMonitor?.Dispose();
        driveMonitor?.Dispose();
        calendarMonitor?.Dispose();
        foregroundWatcher?.Dispose();
        timerMonitor?.Dispose();
        mediaMonitor?.Dispose();
        microphoneMonitor?.Dispose();
        notificationMonitor?.Dispose();
        contextMonitor?.Dispose();
        prayerMonitor?.Dispose();
        weatherMonitor?.Dispose();
        habitMonitor?.Dispose();
        scenarioMonitor?.Dispose();
        clipboardMonitor?.Dispose();
        osdMonitor?.Dispose();
        idleWatcher?.Dispose();
        paletteWindow?.Close();
        cameraWindow?.Close();
        pipe?.Dispose();

        GC.SuppressFinalize(this);
    }
}
