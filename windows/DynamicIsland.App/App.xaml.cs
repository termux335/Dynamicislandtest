using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private Stage5BSettings stage5B = new();
    private Stage5CSettings stage5C = new();
    private TerminalStore? terminalStore;
    private AgentMonitor? agentMonitor;
    private TickerMonitor? tickerMonitor;
    private PluginHost? pluginHost;
    private AssistantWindow? assistantWindow;
    private NoteStore? noteStore;
    private SpeechService? speech;
    private VoiceNoteWindow? voiceWindow;
    private DispatcherTimer? typingTimer;
    private DispatcherTimer? quietTimer;
    private DispatcherTimer? reminderTimer;
    private readonly HashSet<string> announcedReminders = new();
    private IslandViewModel? model;
    private IslandWindow? window;
    private SettingsWindow? settingsWindow;

    private ContextAwarenessSettings contextSettings = new();
    private string? weatherAdm4;
    private OledPhase lastOledPhase = OledPhase.Active;
    private uint? albumRgb;

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
        stage5B = settings.Stage5B ?? new Stage5BSettings();
        stage5C = settings.Stage5C ?? new Stage5CSettings();

        var text = new IslandText(settings.Language, settings.Humor);
        Theme.Apply(settings.Accent);
        Theme.ApplyHighContrast(stage5B.HighContrast, settings.Accent);

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
            NotesEnabled = stage5B.NotesEnabled,
            TypingDeferral = stage5B.TypingDeferral,
            QuietHoursEnabled = stage5B.QuietHoursEnabled,
            VoiceNotes = stage5B.VoiceNotes,
            SpeakNotifications = stage5B.SpeakNotifications,
            LargeText = stage5B.LargeText,
            HighContrast = stage5B.HighContrast,
            AlbumColor = stage5B.AlbumColor,
            Visualizer = stage5B.Visualizer,
            QuietStartHour = stage5B.QuietStartHour,
            QuietEndHour = stage5B.QuietEndHour,
            FocusMusic = stage5B.FocusMusic ?? string.Empty,
            AgentStatusEnabled = stage5C.AgentStatusEnabled,
            TerminalEnabled = stage5C.TerminalEnabled,
            ScreenshotEnabled = stage5C.ScreenshotEnabled,
            TickerEnabled = stage5C.TickerEnabled,
            TickerUrl = stage5C.TickerUrl ?? string.Empty,
            AssistantEnabled = stage5C.AssistantEnabled,
            PluginsEnabled = stage5C.PluginsEnabled,
        };
        this.model = model;

        var timer = new TimerMonitor(controller, Dispatcher, text);
        var media = new MediaMonitor(controller, Dispatcher, text);
        timerMonitor = timer;
        mediaMonitor = media;
        media.ArtworkChanged += bytes => Dispatcher.BeginInvoke(() => ApplyArtwork(bytes));

        var notifications = new NotificationMonitor(controller, Dispatcher, text);
        notificationMonitor = notifications;
        speech = new SpeechService { Enabled = stage5B.SpeakNotifications };
        notifications.Arrived += title => speech?.Speak(title);

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
                case "agent.approve": agentMonitor?.Respond(true); break;
                case "agent.reject": agentMonitor?.Respond(false); break;
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
        timer.FocusStarted += StartFocusMusic;
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
        scenarioMonitor.PlanChanged += _ => UpdateTypingSuppression();
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
        window.NotesRequested += OpenNotes;
        window.VoiceRequested += OpenVoiceNote;

        // Kamera tidak boleh hidup saat island menciut atau disembunyikan.
        controller.Changed += presentation =>
        {
            if (cameraWindow is null) return;
            if (!presentation.Visible || presentation.Primary is { State: not ActivityState.Expanded })
            {
                cameraWindow.Close();
            }
        };

        // Tahap 5b: catatan, tunda saat mengetik, jam tenang, pengingat.
        noteStore = new NoteStore();
        if (noteStore.Load()) Log.Write($"catatan: {noteStore.All.Count} dimuat");

        // Tahap 5c: status agen lokal, ticker opsional, terminal, plugin.
        terminalStore = new TerminalStore();
        if (terminalStore.Load()) Log.Write($"terminal: {terminalStore.All.Count} perintah dimuat");

        agentMonitor = new AgentMonitor(controller, Dispatcher, text) { Enabled = model.AgentStatusEnabled };
        agentMonitor.Start();

        tickerMonitor = new TickerMonitor(controller, Dispatcher, text);
        tickerMonitor.Configure(model.TickerEnabled, model.TickerUrl);
        tickerMonitor.Start();

        pluginHost = new PluginHost(controller);
        pluginHost.Load(model.PluginsEnabled);

        typingTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => UpdateTypingSuppression(),
            Dispatcher);
        typingTimer.Start();

        quietTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => UpdateQuietHours(),
            Dispatcher);
        quietTimer.Start();

        reminderTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => AnnounceReminders(),
            Dispatcher);
        reminderTimer.Start();

        UpdateQuietHours();
        UpdateTypingSuppression();
        AnnounceReminders();

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

    /// <summary>
    /// Warna aksen: kontras tinggi menang, lalu sampul album bila diizinkan,
    /// lalu pilihan pengguna.
    /// </summary>
    private void ApplyAccent()
    {
        if (model is null) return;

        if (model.HighContrast || !model.AlbumColor || albumRgb is not { } rgb)
        {
            Theme.ApplyHighContrast(model.HighContrast, model.Accent);
            return;
        }

        Theme.ApplyRgb(rgb);
    }

    /// <summary>Membaca sampul di UI thread, memilih warna dominan, lalu menerapkannya.</summary>
    private void ApplyArtwork(byte[] bytes)
    {
        if (model is null) return;

        if (bytes.Length == 0)
        {
            albumRgb = null;
            ApplyAccent();
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            albumRgb = AlbumColor.Choose(pixels, converted.PixelWidth, converted.PixelHeight);
            ApplyAccent();
        }
        catch (Exception error) when (error is NotSupportedException or IOException or ArgumentException)
        {
            Log.Write("media: warna sampul gagal dibaca (" + error.Message + ")");
            albumRgb = null;
            ApplyAccent();
        }
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

        if (speech is not null) speech.Enabled = model.SpeakNotifications;
        if (agentMonitor is not null) agentMonitor.Enabled = model.AgentStatusEnabled;
        tickerMonitor?.Configure(model.TickerEnabled, model.TickerUrl);
        pluginHost?.Load(model.PluginsEnabled);
        ApplyAccent();
        UpdateTypingSuppression();
        UpdateQuietHours();
        AnnounceReminders();

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

        var commands = new List<(string Id, string Label)>
        {
            (Id: "media-toggle", Label: text.Get("quickAction.mediaToggle")),
            (Id: "media-next", Label: text.Get("quickAction.mediaNext")),
            (Id: "timer-focus", Label: text.Get("quickAction.timerFocus")),
            (Id: "clipboard-clear", Label: text.Get("menu.clipboardClear")),
            (Id: "camera", Label: text.Get("menu.camera")),
        };

        if (model.ScreenshotEnabled) commands.Add(("screenshot", text.Get("settings.screenshot")));
        if (model.AssistantEnabled) commands.Add(("assistant", text.Get("settings.assistant")));

        var trimmed = query.Trim();

        if (model.NotesEnabled && noteStore is not null)
        {
            const string prefix = "catat ";

            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > prefix.Length)
            {
                var noteText = trimmed[prefix.Length..].Trim();
                entries.Insert(0, new PaletteEntry("note-add", noteText, text.Get("notes.added"), PaletteKind.Command, noteText));
            }

            foreach (var note in noteStore.Search(trimmed).Take(3))
            {
                entries.Add(new PaletteEntry("note:" + note.Id, note.Text, text.Get("notes.title"), PaletteKind.Clipboard, note.Text));
            }
        }

        foreach (var command in commands)
        {
            if (trimmed.Length > 0 && !command.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) continue;

            entries.Add(new PaletteEntry(command.Id, command.Label, text.Get("menu.palette"), PaletteKind.Command, string.Empty));
        }

        if (model.TerminalEnabled && terminalStore is not null)
        {
            foreach (var command in terminalStore.All)
            {
                if (trimmed.Length > 0 && !command.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) continue;

                entries.Add(new PaletteEntry("terminal:" + command.Name, command.Name, "Terminal", PaletteKind.Command, command.Name));
            }
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
                    case "screenshot": CaptureScreenshot(); break;
                    case "assistant": OpenAssistant(); break;
                    case "note-add":
                        if (noteStore is not null)
                        {
                            noteStore.Add(entry.Payload);
                            noteStore.Save();
                            Notify("notes.title", "notes.added");
                        }
                        break;
                    default:
                        if (entry.Id.StartsWith("terminal:", StringComparison.Ordinal))
                        {
                            RunTerminal(entry.Id["terminal:".Length..]);
                        }
                        break;
                }
                break;
        }
    }

    // --- Tahap 5c ---

    private void CaptureScreenshot()
    {
        if (model is null || controller is null || !model.ScreenshotEnabled) return;

        var result = ScreenshotService.Capture();

        controller.Refresh(new Activity(
            id: "screenshot-feedback",
            kind: ActivityKind.Notification,
            priority: 95,
            title: model.Text.Get(result.Ok ? "screenshot.saved" : "screenshot.failed"),
            subtitle: result.Path ?? result.Error,
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddSeconds(6)));
    }

    /// <summary>Hanya perintah terdaftar; selalu konfirmasi bila diminta.</summary>
    private void RunTerminal(string name)
    {
        if (model is null || terminalStore is null) return;
        if (!model.TerminalEnabled)
        {
            Log.Write("terminal: modul dimatikan di pengaturan");
            return;
        }

        var command = TerminalCatalog.Find(terminalStore.All, name);
        if (command is null)
        {
            Log.Write($"terminal: {name} tidak terdaftar");
            return;
        }

        if (command.Confirm)
        {
            var answer = MessageBox.Show(
                $"{command.FileName} {command.Arguments}".Trim(),
                model.Text.Get("terminal.confirm"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(command.FileName, command.Arguments)
            {
                UseShellExecute = false,
            });

            Log.Write($"terminal: menjalankan {command.Name}");
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Write($"terminal: {command.Name} gagal: {error.Message}");
        }
    }

    private void OpenAssistant()
    {
        if (model is null) return;

        if (!model.AssistantEnabled)
        {
            Log.Write("asisten: modul dimatikan di pengaturan");
            return;
        }

        if (assistantWindow is { IsVisible: true })
        {
            assistantWindow.Activate();
            return;
        }

        var window = new AssistantWindow(model.Text, stage5C.AssistantEndpoint, stage5C.AssistantModel);

        window.Closed += (_, _) =>
        {
            stage5C = stage5C with
            {
                AssistantEndpoint = window.Endpoint,
                AssistantModel = window.Model,
            };

            assistantWindow = null;
            SaveSettings();
        };

        assistantWindow = window;
        window.Show();
    }

    // --- Tahap 5b ---

    private void UpdateTypingSuppression()
    {
        if (notificationMonitor is null || model is null) return;

        var typing = model.TypingDeferral && idleWatcher is not null && TypingRules.ShouldHold(idleWatcher.Idle);
        notificationMonitor.SetSuppressed(typing || (scenarioMonitor?.Plan.SuppressNotifications ?? false));
    }

    private void UpdateQuietHours()
    {
        if (controller is null || model is null) return;

        var quiet = QuietHoursRules.IsQuiet(
            DateTimeOffset.Now,
            model.QuietHoursEnabled,
            model.QuietStartHour,
            model.QuietEndHour);

        if (controller.SetQuietHours(quiet))
        {
            Log.Write(quiet ? "jam tenang: aktif" : "jam tenang: selesai");
        }
    }

    private void AnnounceReminders()
    {
        if (noteStore is null || controller is null || model is null) return;

        var now = DateTimeOffset.Now;

        foreach (var note in noteStore.Due(now).Where(note => note.RemindAt is { } at && at >= now.AddHours(-1)))
        {
            if (!announcedReminders.Add(note.Id)) continue;

            controller.Refresh(new Activity(
                id: "note-reminder-" + note.Id,
                kind: ActivityKind.Notification,
                priority: 95,
                title: model.Text.Get("notes.reminderTitle"),
                subtitle: note.Text,
                isLive: false,
                expiresAt: now.AddSeconds(10)));

            speech?.Speak(note.Text);
        }
    }

    private void StartFocusMusic()
    {
        if (model is null) return;
        if (!FocusMusicRules.ShouldStart(true, model.FocusMusic)) return;

        Log.Write("fokus: memutar playlist pilihan pengguna");
        StartPath(model.FocusMusic);
    }

    private void OpenNotes()
    {
        if (noteStore is null) return;

        noteStore.Save();
        StartPath(noteStore.FilePath);
    }

    private void OpenVoiceNote()
    {
        if (model is null) return;

        if (!model.VoiceNotes)
        {
            Log.Write("suara: modul dimatikan di pengaturan");
            return;
        }

        if (voiceWindow is { IsVisible: true })
        {
            voiceWindow.Activate();
            return;
        }

        var window = new VoiceNoteWindow(model.Text);

        window.Accepted += result =>
        {
            if (noteStore is null || result.Text.Length == 0) return;

            noteStore.Add(result.Text, NoteKind.Note, DateTimeOffset.Now, result.RemindAt);
            noteStore.Save();
            Log.Write("suara: catatan disimpan");
            Notify("notes.title", "notes.added");
        };

        window.Closed += (_, _) => voiceWindow = null;
        voiceWindow = window;
        window.Show();
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
            contextSettings with { Enabled = model.ContextEnabled },
            stage5B with
            {
                NotesEnabled = model.NotesEnabled,
                TypingDeferral = model.TypingDeferral,
                QuietHoursEnabled = model.QuietHoursEnabled,
                VoiceNotes = model.VoiceNotes,
                SpeakNotifications = model.SpeakNotifications,
                HighContrast = model.HighContrast,
                LargeText = model.LargeText,
                AlbumColor = model.AlbumColor,
                Visualizer = model.Visualizer,
                QuietStartHour = model.QuietStartHour,
                QuietEndHour = model.QuietEndHour,
                FocusMusic = model.FocusMusic,
            },
            Stage5C: stage5C with
            {
                AgentStatusEnabled = model.AgentStatusEnabled,
                TerminalEnabled = model.TerminalEnabled,
                ScreenshotEnabled = model.ScreenshotEnabled,
                TickerEnabled = model.TickerEnabled,
                TickerUrl = model.TickerUrl,
                AssistantEnabled = model.AssistantEnabled,
                PluginsEnabled = model.PluginsEnabled,
            });

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
        typingTimer?.Stop();
        quietTimer?.Stop();
        reminderTimer?.Stop();
        agentMonitor?.Dispose();
        tickerMonitor?.Dispose();
        pluginHost?.Dispose();
        assistantWindow?.Close();
        speech?.Dispose();
        idleWatcher?.Dispose();
        paletteWindow?.Close();
        cameraWindow?.Close();
        voiceWindow?.Close();
        pipe?.Dispose();

        GC.SuppressFinalize(this);
    }
}
