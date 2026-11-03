using System.ComponentModel;
using System.IO;
using System.Windows;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>Menjembatani <see cref="IslandController"/> ke binding WPF.</summary>
public sealed class IslandViewModel : INotifyPropertyChanged
{
    /// <summary>Satu berkas di shelf; hanya disimpan di memori.</summary>
    public sealed record ShelfFile(string Name, string Path);

    /// <summary>Satu tombol di panel aksi cepat (tekan-tahan 500 ms).</summary>
    public sealed record QuickAction(string Id, string Label);

    /// <summary>Aksi cepat tetap; label mengikuti bahasa dan Mode Kocak.</summary>
    public IReadOnlyList<QuickAction> QuickActions => new[]
    {
        new QuickAction("media-toggle", text.Get("quickAction.mediaToggle")),
        new QuickAction("media-next", text.Get("quickAction.mediaNext")),
        new QuickAction("timer-focus", text.Get("quickAction.timerFocus")),
    };

    private const int MaxShelfFiles = 12;

    private readonly IslandController controller;
    private readonly IslandText text;
    private IslandPresentation presentation;
    private readonly List<string> shelfFiles = new();
    private AccentChoice accent;
    private PositionChoice position;

    public IslandViewModel(
        IslandController controller,
        IslandText? text = null,
        AccentChoice accent = AccentChoice.Oranye,
        PositionChoice position = PositionChoice.TopCenter)
    {
        this.controller = controller;
        this.text = text ?? IslandText.Default;
        this.accent = accent;
        this.position = position;
        presentation = controller.Presentation;
        controller.Changed += next =>
        {
            presentation = next;
            RaiseAll();
        };

        // Preferensi Windows "Perlihatkan animasi di Windows".
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReduceMotion)));
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Menghormati setelan "kurangi gerakan".</summary>
    public bool ReduceMotion => !SystemParameters.ClientAreaAnimation;

    public DisplayMode Mode => presentation.Mode;

    public bool IsStandar => presentation.Mode == DisplayMode.Standar;

    public bool IsRingkas => presentation.Mode == DisplayMode.Ringkas;

    public Activity? Primary => presentation.Primary;

    /// <summary>Aktivitas kedua, tampil sebagai kapsul minimal terpisah (Tahap 3).</summary>
    public Activity? Secondary => presentation.Secondary;

    public bool HasSecondary => Secondary is not null;

    /// <summary>Saklar fitur dua kapsul (Tahap 3) di menu klik kanan.</summary>
    public bool ShowsSecondary => controller.ShowsSecondary;

    public string SecondaryGlyph => Secondary is null ? string.Empty : Glyphs.For(Secondary.Kind);

    public double SecondaryWidth => IslandMetrics.SecondaryFor(presentation.Mode).Width;

    public double SecondaryCornerRadius => SecondaryWidth / 2;

    public string SecondaryAccessibilityLabel
    {
        get
        {
            if (Secondary is not { } activity) return text.GetSerious("a11y.secondaryNone");

            var title = string.IsNullOrEmpty(activity.Title) ? activity.Kind.ToWireName() : activity.Title;
            return text.FormatSerious("a11y.secondary", title);
        }
    }

    // --- Mode tidak mengganggu dan modul Tahap 4 ---

    public IslandContext Context => presentation.Context;

    public bool ContextVisible => presentation.Visible;

    public bool ContextClickThrough => presentation.ClickThrough;

    public double ContextOpacity => presentation.Opacity;

    public bool IsContextActive => presentation.Context != IslandContext.Normal;

    /// <summary>Label konteks untuk pembaca layar; selalu versi serius.</summary>
    public string ContextLabel => text.FormatSerious(
        "a11y.context",
        text.GetSerious(Context switch
        {
            IslandContext.Game => "context.game",
            IslandContext.VideoFullscreen => "context.video",
            IslandContext.Meeting => "context.meeting",
            IslandContext.ScreenShare => "context.share",
            IslandContext.Presentation => "context.presentation",
            _ => "context.normal",
        }));

    private SafePosition? safePosition;

    /// <summary>Posisi aman dari aturan konteks; null berarti posisi biasa.</summary>
    public SafePosition? SafePosition
    {
        get => safePosition;
        set
        {
            if (safePosition == value) return;
            safePosition = value;
            RaiseAll();
        }
    }

    private bool contextEnabled = true;

    public bool ContextEnabled
    {
        get => contextEnabled;
        set
        {
            if (contextEnabled == value) return;
            contextEnabled = value;
            RaiseAll();
        }
    }

    private bool safeShare = true;

    /// <summary>Sembunyikan isi notifikasi saat berbagi atau merekam layar.</summary>
    public bool SafeShare
    {
        get => safeShare;
        set
        {
            if (safeShare == value) return;
            safeShare = value;
            RaiseAll();
        }
    }

    private bool studyMode;

    public bool StudyMode
    {
        get => studyMode;
        set
        {
            if (studyMode == value) return;
            studyMode = value;
            RaiseAll();
        }
    }

    private bool prayerEnabled;

    public bool PrayerEnabled
    {
        get => prayerEnabled;
        set
        {
            if (prayerEnabled == value) return;
            prayerEnabled = value;
            RaiseAll();
        }
    }

    private bool habitsEnabled;

    public bool HabitsEnabled
    {
        get => habitsEnabled;
        set
        {
            if (habitsEnabled == value) return;
            habitsEnabled = value;
            RaiseAll();
        }
    }

    private bool oledProtection = true;

    public bool OledProtection
    {
        get => oledProtection;
        set
        {
            if (oledProtection == value) return;
            oledProtection = value;
            RaiseAll();
        }
    }

    private bool weatherEnabled;

    public bool WeatherEnabled
    {
        get => weatherEnabled;
        set
        {
            if (weatherEnabled == value) return;
            weatherEnabled = value;
            RaiseAll();
        }
    }

    private bool clipboardEnabled;

    public bool ClipboardEnabled
    {
        get => clipboardEnabled;
        set
        {
            if (clipboardEnabled == value) return;
            clipboardEnabled = value;
            RaiseAll();
        }
    }

    private bool paletteEnabled = true;

    public bool PaletteEnabled
    {
        get => paletteEnabled;
        set
        {
            if (paletteEnabled == value) return;
            paletteEnabled = value;
            RaiseAll();
        }
    }

    private bool osdEnabled;

    public bool OsdEnabled
    {
        get => osdEnabled;
        set
        {
            if (osdEnabled == value) return;
            osdEnabled = value;
            RaiseAll();
        }
    }

    private bool mirrorEnabled;

    public bool MirrorEnabled
    {
        get => mirrorEnabled;
        set
        {
            if (mirrorEnabled == value) return;
            mirrorEnabled = value;
            RaiseAll();
        }
    }

    private bool notesEnabled = true;

    public bool NotesEnabled
    {
        get => notesEnabled;
        set
        {
            if (notesEnabled == value) return;
            notesEnabled = value;
            RaiseAll();
        }
    }

    private bool typingDeferral = true;

    public bool TypingDeferral
    {
        get => typingDeferral;
        set
        {
            if (typingDeferral == value) return;
            typingDeferral = value;
            RaiseAll();
        }
    }

    private bool quietHoursEnabled;

    public bool QuietHoursEnabled
    {
        get => quietHoursEnabled;
        set
        {
            if (quietHoursEnabled == value) return;
            quietHoursEnabled = value;
            RaiseAll();
        }
    }

    private bool voiceNotes;

    public bool VoiceNotes
    {
        get => voiceNotes;
        set
        {
            if (voiceNotes == value) return;
            voiceNotes = value;
            RaiseAll();
        }
    }

    private bool speakNotifications;

    public bool SpeakNotifications
    {
        get => speakNotifications;
        set
        {
            if (speakNotifications == value) return;
            speakNotifications = value;
            RaiseAll();
        }
    }

    private bool largeText;

    public bool LargeText
    {
        get => largeText;
        set
        {
            if (largeText == value) return;
            largeText = value;
            RaiseAll();
        }
    }

    private bool highContrast;

    public bool HighContrast
    {
        get => highContrast;
        set
        {
            if (highContrast == value) return;
            highContrast = value;
            RaiseAll();
        }
    }

    /// <summary>Skala teks/aksibilitas; jendela ikut membesar.</summary>
    public double AccessibilityScale => largeText ? 1.25 : 1;

    private bool albumColor;

    /// <summary>Warna aksen mengikuti sampul album yang sedang diputar.</summary>
    public bool AlbumColor
    {
        get => albumColor;
        set
        {
            if (albumColor == value) return;
            albumColor = value;
            RaiseAll();
        }
    }

    private bool visualizer = true;

    public bool Visualizer
    {
        get => visualizer;
        set
        {
            if (visualizer == value) return;
            visualizer = value;
            RaiseAll();
        }
    }

    private int quietStartHour = 22;

    public int QuietStartHour
    {
        get => quietStartHour;
        set
        {
            var next = Math.Clamp(value, 0, 23);
            if (quietStartHour == next) return;
            quietStartHour = next;
            RaiseAll();
        }
    }

    private int quietEndHour = 7;

    public int QuietEndHour
    {
        get => quietEndHour;
        set
        {
            var next = Math.Clamp(value, 0, 23);
            if (quietEndHour == next) return;
            quietEndHour = next;
            RaiseAll();
        }
    }

    private string focusMusic = string.Empty;

    /// <summary>Path/URL playlist fokus pilihan pengguna; kosong berarti tidak diputar.</summary>
    public string FocusMusic
    {
        get => focusMusic;
        set
        {
            var next = value ?? string.Empty;
            if (focusMusic == next) return;
            focusMusic = next;
            RaiseAll();
        }
    }

    public string LabelStage5B => text.Get("settings.stage5b");

    public string LabelNotes => text.Get("settings.notes");

    public string LabelTyping => text.Get("settings.typing");

    public string LabelTypingNote => text.Get("settings.typingNote");

    public string LabelQuiet => text.Get("settings.quiet");

    public string LabelQuietNote => text.Get("settings.quietNote");

    public string LabelVoice => text.Get("settings.voice");

    public string LabelVoiceNote => text.Get("settings.voiceNote");

    public string LabelSpeak => text.Get("settings.speak");

    public string LabelSpeakNote => text.Get("settings.speakNote");

    public string LabelLargeText => text.Get("settings.largeText");

    public string LabelContrast => text.Get("settings.contrast");

    public string LabelAccessibilityNote => text.Get("settings.accessibilityNote");

    public string LabelAlbumColor => text.Get("settings.albumColor");

    public string LabelAlbumColorNote => text.Get("settings.albumColorNote");

    public string LabelVisualizer => text.Get("settings.visualizer");

    public string LabelFocusMusic => text.Get("settings.focusMusic");

    public string LabelFocusMusicNote => text.Get("settings.focusMusicNote");

    public string LabelQuietHoursRange => text.Get("settings.quietHoursRange");

    public string LabelBrowse => text.Get("settings.browse");

    private bool agentStatusEnabled;

    public bool AgentStatusEnabled
    {
        get => agentStatusEnabled;
        set { if (agentStatusEnabled == value) return; agentStatusEnabled = value; RaiseAll(); }
    }

    private bool terminalEnabled;

    public bool TerminalEnabled
    {
        get => terminalEnabled;
        set { if (terminalEnabled == value) return; terminalEnabled = value; RaiseAll(); }
    }

    private bool screenshotEnabled = true;

    public bool ScreenshotEnabled
    {
        get => screenshotEnabled;
        set { if (screenshotEnabled == value) return; screenshotEnabled = value; RaiseAll(); }
    }

    private bool tickerEnabled;

    public bool TickerEnabled
    {
        get => tickerEnabled;
        set { if (tickerEnabled == value) return; tickerEnabled = value; RaiseAll(); }
    }

    private string tickerUrl = string.Empty;

    public string TickerUrl
    {
        get => tickerUrl;
        set { var next = value ?? string.Empty; if (tickerUrl == next) return; tickerUrl = next; RaiseAll(); }
    }

    private bool assistantEnabled;

    public bool AssistantEnabled
    {
        get => assistantEnabled;
        set { if (assistantEnabled == value) return; assistantEnabled = value; RaiseAll(); }
    }

    private bool pluginsEnabled;

    public bool PluginsEnabled
    {
        get => pluginsEnabled;
        set { if (pluginsEnabled == value) return; pluginsEnabled = value; RaiseAll(); }
    }

    public string LabelStage5C => text.Get("settings.stage5c");

    public string LabelAgentStatus => text.Get("settings.agentStatus");

    public string LabelAgentStatusNote => text.Get("settings.agentStatusNote");

    public string LabelTerminal => text.Get("settings.terminal");

    public string LabelTerminalNote => text.Get("settings.terminalNote");

    public string LabelScreenshot => text.Get("settings.screenshot");

    public string LabelScreenshotNote => text.Get("settings.screenshotNote");

    public string LabelTicker => text.Get("settings.ticker");

    public string LabelTickerNote => text.Get("settings.tickerNote");

    public string LabelTickerUrl => text.Get("settings.tickerUrl");

    public string LabelAssistant => text.Get("settings.assistant");

    public string LabelAssistantNote => text.Get("settings.assistantNote");

    public string LabelPlugins => text.Get("settings.plugins");

    public string LabelPluginsNote => text.Get("settings.pluginsNote");

    public string MenuNotes => text.Get("menu.notes");

    public string MenuVoice => text.Get("menu.voice");

    private IReadOnlyList<Shortcut> shortcuts = Array.Empty<Shortcut>();

    /// <summary>Pintasan aplikasi/folder yang didaftarkan pengguna (Tahap 5a).</summary>
    public IReadOnlyList<Shortcut> Shortcuts
    {
        get => shortcuts;
        set
        {
            shortcuts = value ?? Array.Empty<Shortcut>();
            RaiseAll();
        }
    }

    public bool HasShortcuts => shortcuts.Count > 0;

    public string MenuContext => text.Get("menu.context");

    public string MenuSafeShare => text.Get("menu.safeShare");

    public string MenuStudy => text.Get("menu.study");

    public string LabelContextTitle => text.Get("settings.contextTitle");

    public string LabelContextEnabled => text.Get("menu.context");

    public string LabelContextNote => text.Get("settings.contextNote");

    public string LabelSafeShare => text.Get("settings.safeShare");

    public string LabelSafeShareNote => text.Get("settings.safeShareNote");

    public string LabelPrayer => text.Get("settings.prayer");

    public string LabelPrayerNote => text.Get("settings.prayerNote");

    public string LabelHabits => text.Get("settings.habits");

    public string LabelHabitsNote => text.Get("settings.habitsNote");

    public string LabelOled => text.Get("settings.oled");

    public string LabelWeather => text.Get("settings.weather");

    public string LabelWeatherNote => text.Get("settings.weatherNote");

    public string LabelClipboard => text.Get("settings.clipboard");

    public string LabelClipboardNote => text.Get("settings.clipboardNote");

    public string LabelPalette => text.Get("settings.palette");

    public string LabelOsd => text.Get("settings.osd");

    public string LabelOsdNote => text.Get("settings.osdNote");

    public string LabelMirror => text.Get("settings.mirror");

    public string LabelMirrorNote => text.Get("settings.mirrorNote");

    public string LabelModulesNote => text.Get("settings.modulesNote");

    public string MenuPalette => text.Get("menu.palette");

    public string MenuClipboard => text.Get("menu.clipboard");

    public string MenuClipboardClear => text.Get("menu.clipboardClear");

    public string MenuCamera => text.Get("menu.camera");

    public string MenuShortcuts => text.Get("menu.shortcuts");

    // --- Penempatan adaptif (Tahap 3) ---

    private ForegroundInfo? foreground;
    private bool autoPlacement = true;

    /// <summary>Saklar menu: geser kapsul saat game/peramban menutup layar penuh.</summary>
    public bool AutoPlacement
    {
        get => autoPlacement;
        set
        {
            if (autoPlacement == value) return;
            autoPlacement = value;
            RaiseAll();
        }
    }

    /// <summary>Posisi yang dipilih aturan untuk window depan sekarang.</summary>
    public PlacementAnchor Anchor => PlacementRules.Choose(foreground, autoPlacement, position);

    /// <summary>Dipanggil <see cref="ForegroundWatcher"/> saat window depan berubah.</summary>
    internal void SetForeground(ForegroundInfo? info)
    {
        if (foreground == info) return;
        foreground = info;
        RaiseAll();
    }

    public ActivityState? PrimaryState => Primary?.State;

    public bool HasActivity => Primary is not null;

    public bool IsIdle => Primary is null;

    public bool IsMinimal => Primary?.State == ActivityState.Minimal;

    public bool IsCompact => Primary?.State == ActivityState.Compact;

    public bool IsExpanded => Primary?.State == ActivityState.Expanded;

    public string Title => Primary?.Title ?? string.Empty;

    public string Subtitle => Primary?.Subtitle ?? string.Empty;

    public bool HasSubtitle => !string.IsNullOrEmpty(Primary?.Subtitle);

    public bool HasProgress => Primary?.Progress is not null;

    public double ProgressPercent => (Primary?.Progress ?? 0) * 100;

    public string ProgressLabel => $"{Math.Round((Primary?.Progress ?? 0) * 100)}%";

    /// <summary>Sisi kanan kapsul compact: progres bila ada, kalau tidak subjudul.</summary>
    public string CompactTrailing => HasProgress ? ProgressLabel : Subtitle;

    // --- Kartu media: kontrol, timeline, dan lirik ---

    public bool IsMedia => Primary?.Kind == ActivityKind.Media;

    /// <summary>Status putar dibaca dari glyph aksi yang dipakai MediaMonitor.</summary>
    public bool MediaPlaying =>
        Primary?.Actions.FirstOrDefault(action => action.Id == "media-toggle")?.Glyph == "pause";

    /// <summary>Glyph Segoe MDL2 Assets: putar E768, jeda E769.</summary>
    public string MediaToggleGlyph => MediaPlaying ? "\uE769" : "\uE768";

    public string MediaPreviousGlyph => "\uE892";

    public string MediaNextGlyph => "\uE893";

    public string MediaToggleLabel => MediaPlaying ? LabelMediaPause : LabelMediaPlay;

    public string MediaElapsed => FormatTime(Primary?.Position);

    public string MediaDuration => FormatTime(Primary?.Duration);

    public bool HasMediaTimes => MediaElapsed.Length > 0 || MediaDuration.Length > 0;

    /// <summary>Baris lirik yang sedang berlaku, bila berkas .lrc-nya ada.</summary>
    public string Caption => Primary?.Caption ?? string.Empty;

    public bool HasCaption => !string.IsNullOrEmpty(Primary?.Caption);

    /// <summary>Jam:menit:detik untuk timeline; kosong bila waktunya tidak diketahui.</summary>
    private static string FormatTime(TimeSpan? value)
    {
        if (value is not { } span || span < TimeSpan.Zero) return string.Empty;

        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }

    public string Glyph => Primary is null ? string.Empty : Glyphs.For(Primary.Kind);

    /// <summary>Aksi aktivitas plus dua pintasan aplikasi di kartu expanded.</summary>
    public IReadOnlyList<ActivityAction> Actions =>
        (Primary?.Actions ?? Array.Empty<ActivityAction>())
            .Concat(Shortcuts.Take(2).Select(shortcut => new ActivityAction("shortcut:" + shortcut.Id, shortcut.Label, "app")))
            .ToList();

    public bool HasActions => Actions.Count > 0;

    /// <summary>Ukuran kapsul untuk state sekarang; idle memakai bentuk minimal.</summary>
    public CapsuleSize Size => IsQuickActionsOpen
        ? IslandMetrics.QuickActions
        : IsShelfOpen
            ? IslandMetrics.Shelf
            : Primary is null
                ? IslandMetrics.Idle(presentation.Mode)
                : IslandMetrics.For(Primary, presentation.Mode);

    /// <summary>Lebar window: kapsul utama plus kapsul kedua dan jaraknya bila tampil.</summary>
    public double WindowWidth => Size.Width + (ShowSecondaryCapsule ? SecondaryWidth + IslandMetrics.SecondaryGap : 0);

    /// <summary>Panel shelf dan aksi cepat menutupi kapsul, jadi isi kapsul ikut disembunyikan.</summary>
    public bool OverlayOpen => IsShelfOpen || IsQuickActionsOpen;

    public bool ShowsIdleLayer => IsIdle && !OverlayOpen;

    public bool ShowsMinimalLayer => IsMinimal && !OverlayOpen;

    public bool ShowsCompactLayer => IsCompact && !OverlayOpen;

    public bool ShowsExpandedLayer => IsExpanded && !OverlayOpen;

    /// <summary>Kartu media memakai tata letak sendiri: judul, lirik, timeline, kontrol.</summary>
    public bool ShowsMediaCard => ShowsExpandedLayer && IsMedia;

    /// <summary>Kartu umum untuk timer, rapat, notifikasi, unduhan, dan kalender.</summary>
    public bool ShowsGenericCard => ShowsExpandedLayer && !IsMedia;

    /// <summary>Kapsul kedua disembunyikan saat panel shelf atau aksi cepat terbuka.</summary>
    public bool ShowSecondaryCapsule => HasSecondary && !OverlayOpen;

    /// <summary>Expanded melengkung hanya di sudut bawah, compact/minimal membulat penuh.</summary>
    public CornerRadius CapsuleCornerRadius => IsShelfOpen || IsQuickActionsOpen
        ? new CornerRadius(Size.CornerRadius)
        : Primary?.State switch
        {
            ActivityState.Minimal => new CornerRadius(Size.Width / 2),
            ActivityState.Compact => new CornerRadius(Size.Height / 2),
            ActivityState.Expanded => new CornerRadius(0, 0, Size.CornerRadius, Size.CornerRadius),
            _ => new CornerRadius(Size.Width / 2),
        };

    // --- Shelf berkas (Tahap 2, khusus laptop) ---

    public bool IsShelfOpen { get; private set; }

    public IReadOnlyList<ShelfFile> ShelfFiles =>
        shelfFiles.Select(path => new ShelfFile(Path.GetFileName(path), path)).ToList();

    public bool HasShelfFiles => shelfFiles.Count > 0;

    /// <summary>Dua berkas pertama yang tampil di kapsul; sisanya lewat hitungan.</summary>
    public IReadOnlyList<ShelfFile> ShelfVisibleFiles => ShelfFiles.Take(2).ToList();

    public string ShelfHeadline => shelfFiles.Count == 0
        ? text.Get("shelf.empty")
        : text.Format("shelf.count", shelfFiles.Count);

    public void ToggleShelf()
    {
        IsShelfOpen = !IsShelfOpen;
        IsQuickActionsOpen = false;
        RaiseAll();
    }

    // --- Aksi cepat tekan-tahan (Tahap 3) ---

    public bool IsQuickActionsOpen { get; private set; }

    public void ToggleQuickActions()
    {
        IsQuickActionsOpen = !IsQuickActionsOpen;
        if (IsQuickActionsOpen) IsShelfOpen = false;
        RaiseAll();
    }

    public void CloseQuickActions()
    {
        if (!IsQuickActionsOpen) return;

        IsQuickActionsOpen = false;
        RaiseAll();
    }

    /// <summary>Menambah berkas ke shelf dan membukanya. Mengembalikan jumlah yang ditambahkan.</summary>
    public int AddShelfFiles(IEnumerable<string> paths)
    {
        var added = 0;

        foreach (var path in paths)
        {
            if (shelfFiles.Count >= MaxShelfFiles) break;
            if (string.IsNullOrWhiteSpace(path) || shelfFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;

            shelfFiles.Add(path);
            added++;
        }

        if (added == 0) return 0;

        IsShelfOpen = true;
        RaiseAll();
        return added;
    }

    public void ClearShelf()
    {
        if (shelfFiles.Count == 0) return;

        shelfFiles.Clear();
        RaiseAll();
    }

    /// <summary>Label untuk screen reader: judul, subjudul, progres, state.</summary>
    public string AccessibilityLabel
    {
        get
        {
            if (Primary is not { } activity) return text.GetSerious("a11y.idle");

            var parts = new List<string> { string.IsNullOrEmpty(activity.Title) ? activity.Kind.ToWireName() : activity.Title };
            if (!string.IsNullOrEmpty(activity.Subtitle)) parts.Add(activity.Subtitle);
            if (activity.Progress is { } progress) parts.Add(text.FormatSerious("a11y.progress", Math.Round(progress * 100)));
            parts.Add(text.GetSerious($"a11y.state.{activity.State.ToWireName()}"));
            return string.Join(", ", parts);
        }
    }

    // --- Kustomisasi dan lokalisasi (Tahap 3) ---

    /// <summary>Katalog teks aktif; dibagi dengan monitor supaya perubahan langsung berlaku.</summary>
    public IslandText Text => text;

    public DynamicIsland.Core.Language Language => text.Language;

    public HumorMode Humor => text.Humor;

    /// <summary>Aksen warna pilihan pengguna; mengubah brush aplikasi secara langsung.</summary>
    public AccentChoice Accent
    {
        get => accent;
        set
        {
            if (accent == value) return;
            accent = value;
            Theme.Apply(accent);
            RaiseAll();
        }
    }

    /// <summary>Posisi tenang pilihan pengguna; penempatan adaptif tetap bisa menimpanya.</summary>
    public PositionChoice Position
    {
        get => position;
        set
        {
            if (position == value) return;
            position = value;
            RaiseAll();
        }
    }

    public void SetLanguage(DynamicIsland.Core.Language language)
    {
        if (text.Language == language) return;
        text.Use(language, text.Humor);
        RaiseAll();
    }

    public void SetHumor(HumorMode humor)
    {
        if (text.Humor == humor) return;
        text.Use(text.Language, humor);
        RaiseAll();
    }

    public void SetPriority(PriorityPreset preset)
    {
        if (controller.PriorityPreset == preset) return;
        controller.PriorityPreset = preset;
        RaiseAll();
    }

    public bool IsLanguageId => text.Language == DynamicIsland.Core.Language.Id;

    public bool IsLanguageEn => text.Language == DynamicIsland.Core.Language.En;

    public bool IsHumorKocak => text.Humor == HumorMode.Kocak;

    public bool IsPositionTopCenter => position == PositionChoice.TopCenter;

    public bool IsPositionTopLeft => position == PositionChoice.TopLeft;

    public bool IsPositionTopRight => position == PositionChoice.TopRight;

    public bool IsAccentOranye => accent == AccentChoice.Oranye;

    public bool IsAccentBiru => accent == AccentChoice.Biru;

    public bool IsAccentUngu => accent == AccentChoice.Ungu;

    public bool IsAccentHijau => accent == AccentChoice.Hijau;

    public bool IsPriorityBawaan => controller.PriorityPreset == PriorityPreset.Bawaan;

    public bool IsPriorityMedia => controller.PriorityPreset == PriorityPreset.MediaUtama;

    public bool IsPriorityTimer => controller.PriorityPreset == PriorityPreset.TimerUtama;

    private bool startWithWindows;

    /// <summary>Mulai otomatis saat login (entri HKCU Run).</summary>
    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (startWithWindows == value) return;
            startWithWindows = value;
            RaiseAll();
        }
    }

    public string LabelSettingsTitle => text.Get("settings.title");

    public string LabelWelcomeTitle => text.Get("settings.welcomeTitle");

    public string LabelSettingsIntro => text.Get("settings.intro");

    public string LabelHumorNote => text.Get("settings.humorNote");

    public string LabelAutostart => text.Get("settings.autostart");

    public string LabelAutostartNote => text.Get("settings.autostartNote");

    public string LabelAutostartFailed => text.Get("settings.autostartFailed");

    public string LabelPermissions => text.Get("settings.permissions");

    public string LabelClose => text.Get("settings.close");

    public string LabelStart => text.Get("settings.start");

    /// <summary>Versi build di kaki jendela pengaturan; selalu versi serius.</summary>
    public string LabelVersion => text.FormatSerious("settings.version", AppInfo.Version);

    private bool notificationsEnabled;

    /// <summary>Saklar fitur notifikasi Windows (izin diminta saat dinyalakan).</summary>
    public bool NotificationsEnabled
    {
        get => notificationsEnabled;
        set
        {
            if (notificationsEnabled == value) return;
            notificationsEnabled = value;
            RaiseAll();
        }
    }

    public string MenuDisplayMode => text.Get("menu.displayMode");

    public string MenuStandard => text.Get("menu.standard");

    public string MenuCompact => text.Get("menu.compact");

    public string MenuSecondary => text.Get("menu.secondary");

    public string MenuAutoPlacement => text.Get("menu.autoPlacement");

    public string MenuQuickActions => text.Get("menu.quickActions");

    public string MenuTimer => text.Get("menu.timer");

    public string MenuFocus25 => text.Get("menu.focus25");

    public string MenuStopwatch => text.Get("menu.stopwatch");

    public string MenuTimerStop => text.Get("menu.timerStop");

    public string MenuShelf => text.Get("menu.shelf");

    public string MenuShelfClear => text.Get("menu.shelfClear");

    public string MenuLanguage => text.Get("menu.language");

    public string MenuHumor => text.Get("menu.humor");

    public string MenuNotifications => text.Get("menu.notifications");

    public string MenuSettings => text.Get("menu.settings");

    public string MenuAccent => text.Get("menu.accent");

    public string MenuPosition => text.Get("menu.position");

    public string MenuPriority => text.Get("menu.priority");

    public string MenuQuit => text.Get("menu.quit");

    public string LabelLanguageId => text.Get("language.id");

    public string LabelLanguageEn => text.Get("language.en");

    public string LabelPositionTopCenter => text.Get("position.topCenter");

    public string LabelPositionTopLeft => text.Get("position.topLeft");

    public string LabelPositionTopRight => text.Get("position.topRight");

    public string LabelAccentOrange => text.Get("accent.orange");

    public string LabelAccentBlue => text.Get("accent.blue");

    public string LabelAccentPurple => text.Get("accent.purple");

    public string LabelAccentGreen => text.Get("accent.green");

    public string LabelPriorityDefault => text.Get("priority.bawaan");

    public string LabelPriorityMedia => text.Get("priority.media");

    public string LabelPriorityTimer => text.Get("priority.timer");

    public string LabelMediaPlay => text.Get("media.play");

    public string LabelMediaPause => text.Get("media.pause");

    public string LabelMediaPrevious => text.Get("media.previous");

    public string LabelMediaNext => text.Get("media.next");

    public string LabelMediaLyrics => text.Get("media.lyrics");

    internal void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

internal static class Glyphs
{
    /// <summary>Glyph monokrom sederhana; dipetakan per kind.</summary>
    public static string For(ActivityKind kind) => kind switch
    {
        ActivityKind.Media => "\u266A",
        ActivityKind.Timer => "\u25F7",
        ActivityKind.Call => "\u260E",
        ActivityKind.Download => "\u2193",
        ActivityKind.Order => "\u25A4",
        ActivityKind.Calendar => "\u25A6",
        ActivityKind.Battery => "\u25AE",
        ActivityKind.Device => "\u2248",
        _ => "\u25CF",
    };
}
