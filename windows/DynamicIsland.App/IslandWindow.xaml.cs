using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

public partial class IslandWindow : Window
{
    private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(320);

    /// <summary>Tekan-tahan selama ini membuka panel aksi cepat.</summary>
    private static readonly TimeSpan LongPressDuration = TimeSpan.FromMilliseconds(500);

    private readonly IslandViewModel model;
    private readonly IslandController controller;
    private readonly TimerMonitor timer;
    private readonly MediaMonitor media;
    private readonly NotificationMonitor notifications;
    private readonly DispatcherTimer longPressTimer;
    private readonly DispatcherTimer visualizerTimer;
    private readonly FrameworkElement[] visualizerBars;

    private DispatcherTimer? resizeTimer;
    private DateTime resizeStarted;
    private double startWidth, startHeight, targetWidth, targetHeight;
    private double startLeft, startTop, targetLeft, targetTop;
    private double lastTargetWidth = double.NaN;
    private double lastTargetHeight = double.NaN;
    private PlacementAnchor? lastAnchor;
    private SafePosition? lastSafePosition;
    private bool longPressFired;

    private HotkeyHost? hotkeys;
    private bool clickThrough;
    private bool oledVisible = true;
    private double oledOpacity = 1;
    private int oledOffsetX;
    private int oledOffsetY;
    private double accessibilityScale = 1;
    private readonly TickleDetector tickleDetector = new();
    private readonly EasterEggSequence easterEgg = new("island");
    private DispatcherTimer? tickleWobbleTimer;
    private DateTime tickleWobbleStarted;

    public IslandWindow(
        IslandViewModel model,
        IslandController controller,
        TimerMonitor timer,
        MediaMonitor media,
        NotificationMonitor notifications)
    {
        InitializeComponent();

        this.model = model;
        this.controller = controller;
        this.timer = timer;
        this.media = media;
        this.notifications = notifications;
        DataContext = model;

        // Konstruktor 4 argumen langsung menyalakan timer; tekan-tahan dimulai di MouseDown.
        longPressTimer = new DispatcherTimer(
            LongPressDuration,
            DispatcherPriority.Input,
            (_, _) => OnLongPress(),
            Dispatcher);
        longPressTimer.Stop();

        visualizerBars = new FrameworkElement[]
        {
            VisualizerBar1, VisualizerBar2, VisualizerBar3, VisualizerBar4, VisualizerBar5,
        };

        visualizerTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(70),
            DispatcherPriority.Render,
            (_, _) => AnimateVisualizer(),
            Dispatcher);
        visualizerTimer.Start();

        model.PropertyChanged += (_, _) => SyncBounds(animate: true);
        Loaded += (_, _) => SyncBounds(animate: false);
    }

    /// <summary>Dipanggil saat tombol aksi di kartu expanded ditekan.</summary>
    public event Action<ActivityAction>? ActionInvoked;

    /// <summary>Dipanggil setelah pengguna mengubah mode, dua kapsul, atau penempatan.</summary>
    public event Action? SettingsChanged;

    /// <summary>Dipanggil saat menu "Pengaturan…" dipilih; App membuka jendelanya.</summary>
    public event Action? SettingsRequested;

    /// <summary>Pintasan global ditekan (Tahap 4); App yang menjalankan aksinya.</summary>
    public event Action<IslandHotkey>? HotkeyPressed;

    /// <summary>Menu Tahap 5a; App yang membuka palette, kamera, atau berkas pintasan.</summary>
    public event Action? PaletteRequested;

    public event Action? ClipboardRequested;

    public event Action? ClipboardClearRequested;

    public event Action? CameraRequested;

    public event Action? ShortcutsRequested;

    public event Action? NotesRequested;

    public event Action? VoiceRequested;

    public event Action? ScreenshotRequested;

    private void Notes_Click(object sender, RoutedEventArgs e) => NotesRequested?.Invoke();

    private void Voice_Click(object sender, RoutedEventArgs e) => VoiceRequested?.Invoke();

    private void Palette_Click(object sender, RoutedEventArgs e) => PaletteRequested?.Invoke();

    private void Clipboard_Click(object sender, RoutedEventArgs e) => ClipboardRequested?.Invoke();

    private void ClipboardClear_Click(object sender, RoutedEventArgs e) => ClipboardClearRequested?.Invoke();

    private void Camera_Click(object sender, RoutedEventArgs e) => CameraRequested?.Invoke();

    private void Shortcuts_Click(object sender, RoutedEventArgs e) => ShortcutsRequested?.Invoke();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        hotkeys = new HotkeyHost(this);
        hotkeys.Pressed += key => HotkeyPressed?.Invoke(key);
    }

    protected override void OnClosed(EventArgs e)
    {
        visualizerTimer.Stop();
        hotkeys?.Detach();
        hotkeys = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// Batang dekoratif yang bergerak saat lagu diputar. Ini bukan analisis
    /// audio; hanya indikator visual supaya kartu media terasa hidup.
    /// </summary>
    private void AnimateVisualizer()
    {
        if (model.ShowCapsuleEyes)
        {
            var (cx, cy) = CursorInDip();
            var centerX = Left + (ActualWidth > 0 ? ActualWidth / 2 : 17);
            var centerY = Top + (ActualHeight > 0 ? ActualHeight / 2 : 17);
            var (ox, oy) = EyeGaze.Calculate(centerX, centerY, cx, cy);
            model.UpdatePupils(ox, oy);
        }

        if (model.IsHumorKocak && (model.HumorBehaviors?.DramaticCountdown ?? true) && timer.IsRunning)
        {
            var remaining = timer.Remaining;
            if (DramaTimer.IsDramatic(remaining))
            {
                var freq = DramaTimer.PulseFrequency(remaining);
                var pulseSeconds = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
                var pulseScale = 1.0 + (0.04 * Math.Sin(pulseSeconds * freq * 2 * Math.PI));
                Capsule.RenderTransform = new ScaleTransform(pulseScale, pulseScale, ActualWidth / 2, ActualHeight / 2);
            }
            else if (Capsule.RenderTransform is ScaleTransform)
            {
                Capsule.RenderTransform = null;
            }
        }
        else if (Capsule.RenderTransform is ScaleTransform)
        {
            Capsule.RenderTransform = null;
        }

        var active = model.Visualizer && model.IsMedia && model.MediaPlaying && model.ShowsMediaCard;
        var visibility = active ? Visibility.Visible : Visibility.Collapsed;

        if (VisualizerPanel.Visibility != visibility) VisualizerPanel.Visibility = visibility;
        if (!active) return;

        var seconds = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;

        for (var index = 0; index < visualizerBars.Length; index++)
        {
            var level = 3 + (15 * Math.Abs(Math.Sin((seconds * 2.6) + (index * 0.9))));
            visualizerBars[index].Height = level;
        }
    }

    /// <summary>
    /// Menerapkan pelindung layar OLED: geser mikro, redup saat idle, sembunyi
    /// setelah idle lama. Tidak pernah mengubah isi aktivitas.
    /// </summary>
    public void ApplyOled(OledState state)
    {
        if (oledVisible == state.Visible
            && Math.Abs(oledOpacity - state.Opacity) < 0.001
            && oledOffsetX == state.OffsetX
            && oledOffsetY == state.OffsetY)
        {
            return;
        }

        oledVisible = state.Visible;
        oledOpacity = state.Opacity;
        oledOffsetX = state.OffsetX;
        oledOffsetY = state.OffsetY;
        SyncBounds(animate: true);
    }

    /// <summary>Menyesuaikan ukuran dan posisi kapsul dengan state sekarang.</summary>
    private void SyncBounds(bool animate)
    {
        // Konteks tidak mengganggu bisa menyembunyikan kapsul sepenuhnya.
        if (!model.ContextVisible || !oledVisible)
        {
            if (IsVisible) Hide();
            return;
        }

        if (!IsVisible) Show();

        var opacity = Math.Min(model.ContextOpacity, oledOpacity);
        if (Math.Abs(Opacity - opacity) > 0.001) Opacity = opacity;
        SetClickThrough(model.ContextClickThrough);

        var size = model.Size;
        var scale = model.AccessibilityScale;

        if (Math.Abs(accessibilityScale - scale) > 0.001)
        {
            accessibilityScale = scale;
            RootGrid.LayoutTransform = new ScaleTransform(scale, scale);
        }

        RootGrid.Width = model.WindowWidth;
        RootGrid.Height = size.Height;

        var width = model.WindowWidth * scale;
        var height = size.Height * scale;
        var (left, top) = TargetFor(width, height);

        // Perubahan isi (mis. progres media) tidak perlu menggeser window.
        if (IsVisible
            && width == lastTargetWidth
            && size.Height == lastTargetHeight
            && model.Anchor == lastAnchor
            && model.SafePosition == lastSafePosition)
        {
            return;
        }

        lastTargetWidth = width;
        lastTargetHeight = size.Height;
        lastAnchor = model.Anchor;
        lastSafePosition = model.SafePosition;

        if (animate && !model.ReduceMotion && IsVisible)
        {
            BeginResize(width, size.Height, left, top);
        }
        else
        {
            resizeTimer?.Stop();
            ApplyBounds(left, top, width, size.Height);
        }
    }

    /// <summary>
    /// Grup kapsul menempel di tengah atas layar utama, 8 px dari tepi atas;
    /// kapsul kedua (bila ada) berada di sebelah kanan kapsul utama. Saat window
    /// depan menutup layar dengan bilah di atas, kapsul pindah ke kiri tengah
    /// supaya tidak menutupi tab, bilah alamat, atau HUD game.
    /// </summary>
    private (double Left, double Top) TargetFor(double width, double height)
    {
        var area = SystemParameters.WorkArea;
        var inset = IslandMetrics.FloatingTopInset;
        var screen = new ScreenArea(area.Left, area.Top, area.Width, area.Height);

        var (left, top) = model.SafePosition is { } safe
            ? SafePlacement.Spot(screen, width, height, safe)
            : model.Anchor switch
            {
                PlacementAnchor.MidLeft => (area.Left + inset, area.Top + ((area.Height - height) / 2)),
                PlacementAnchor.TopLeft => (area.Left + inset, area.Top + inset),
                PlacementAnchor.TopRight => (area.Right - width - inset, area.Top + inset),
                _ => (area.Left + ((area.Width - width) / 2), area.Top + inset),
            };

        // Hindari kursor (desktop), lalu geseran mikro OLED.
        var (cursorX, cursorY) = CursorInDip();
        var (dodgeX, dodgeY) = SafePlacement.Dodge(new ScreenArea(left, top, width, height), cursorX, cursorY);

        left = Math.Clamp(left + dodgeX + oledOffsetX, area.Left, Math.Max(area.Left, area.Right - width));
        top = Math.Clamp(top + dodgeY + oledOffsetY, area.Top, Math.Max(area.Top, area.Bottom - height));

        return (left, top);
    }

    private (double X, double Y) CursorInDip()
    {
        if (!GetCursorPos(out var point)) return (-10_000, -10_000);

        try
        {
            var dip = PointFromScreen(new Point(point.X, point.Y));
            return (dip.X, dip.Y);
        }
        catch (InvalidOperationException)
        {
            return (-10_000, -10_000);
        }
    }

    /// <summary>Menembus klik saat konteks meminta; gaya window biasa, tanpa hook.</summary>
    private void SetClickThrough(bool enabled)
    {
        if (clickThrough == enabled) return;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style = enabled ? style | WsExTransparent | WsExLayered : style & ~WsExTransparent;

        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
        clickThrough = enabled;
    }

    private void ApplyBounds(double left, double top, double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Left = left;
        Top = top;
    }

    private void BeginResize(double width, double height, double left, double top)
    {
        var shown = IsVisible && ActualWidth > 0;

        startWidth = shown ? ActualWidth : width;
        startHeight = shown ? ActualHeight : height;
        startLeft = shown ? Left : left;
        startTop = shown ? Top : top;
        targetWidth = width;
        targetHeight = height;
        targetLeft = left;
        targetTop = top;
        resizeStarted = DateTime.UtcNow;

        resizeTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnResizeTick,
            Dispatcher);

        resizeTimer.Start();
    }

    private void OnResizeTick(object? sender, EventArgs e)
    {
        var progress = (DateTime.UtcNow - resizeStarted).TotalMilliseconds / ResizeDuration.TotalMilliseconds;

        if (progress >= 1)
        {
            resizeTimer!.Stop();
            ApplyBounds(targetLeft, targetTop, targetWidth, targetHeight);
            return;
        }

        var eased = EaseOutBack(progress);

        // Posisi memakai easing tanpa overshoot supaya kapsul tidak sempat keluar layar.
        var slide = Math.Clamp(eased, 0, 1);

        ApplyBounds(
            Lerp(startLeft, targetLeft, slide),
            Lerp(startTop, targetTop, slide),
            Lerp(startWidth, targetWidth, eased),
            Lerp(startHeight, targetHeight, eased));
    }

    /// <summary>Pegas ringan: sedikit melewati target lalu kembali.</summary>
    private static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        var p = t - 1;
        return 1 + (c3 * p * p * p) + (c1 * p * p);
    }

    private static double Lerp(double from, double to, double factor) => from + ((to - from) * factor);

    private void Capsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;

        if (model.IsHumorKocak && (model.HumorBehaviors?.Tickle ?? true))
        {
            if (tickleDetector.RegisterClick(DateTimeOffset.UtcNow))
            {
                longPressTimer.Stop();
                TriggerTickle();
                e.Handled = true;
                return;
            }
        }

        // Mulai hitung tekan-tahan; klik biasa berhenti di MouseLeftButtonUp.
        longPressFired = false;
        longPressTimer.Stop();
        longPressTimer.Start();
    }

    private void Capsule_MouseLeave(object sender, MouseEventArgs e) => longPressTimer.Stop();

    private void OnLongPress()
    {
        longPressTimer.Stop();
        longPressFired = true;
        model.ToggleQuickActions();
        Log.Write($"aksi cepat: tekan-tahan (buka: {model.IsQuickActionsOpen})");
    }

    /// <summary>Gulir di kapsul: ke atas lagu sebelumnya, ke bawah lagu berikutnya.</summary>
    private void Capsule_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) media.Previous();
        else media.Next();

        Log.Write($"media: gulir {(e.Delta > 0 ? "sebelumnya" : "berikutnya")}");
        e.Handled = true;
    }

    private void Capsule_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        longPressTimer.Stop();

        if (longPressFired)
        {
            longPressFired = false;
            e.Handled = true;
            return;
        }

        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;

        // Saat panel aksi cepat terbuka, klik di area kosong menutupnya.
        if (model.IsQuickActionsOpen)
        {
            model.CloseQuickActions();
            e.Handled = true;
            return;
        }

        // Saat shelf terbuka, klik di area kosong menutup shelf.
        if (model.IsShelfOpen)
        {
            model.ToggleShelf();
            e.Handled = true;
            return;
        }

        Toggle();
        e.Handled = true;
    }

    /// <summary>Klik kapsul minimal kedua: aktivitas itu naik jadi utama.</summary>
    private void SecondaryCapsule_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (controller.PromoteSecondary())
        {
            Log.Write($"kapsul kedua dipromosikan: {model.Primary?.Id}");
        }

        e.Handled = true;
    }

    private void AutoPlacement_Click(object sender, RoutedEventArgs e)
    {
        model.AutoPlacement = !model.AutoPlacement;
        SettingsChanged?.Invoke();
    }

    private void ModeStandar_Click(object sender, RoutedEventArgs e)
    {
        controller.DisplayMode = DisplayMode.Standar;
        SettingsChanged?.Invoke();
    }

    private void ModeRingkas_Click(object sender, RoutedEventArgs e)
    {
        controller.DisplayMode = DisplayMode.Ringkas;
        SettingsChanged?.Invoke();
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        controller.ShowsSecondary = !controller.ShowsSecondary;
        SettingsChanged?.Invoke();
    }

    // --- Kustomisasi dan lokalisasi (Tahap 3) ---

    private void PositionTopCenter_Click(object sender, RoutedEventArgs e) => SetPosition(PositionChoice.TopCenter);

    private void PositionTopLeft_Click(object sender, RoutedEventArgs e) => SetPosition(PositionChoice.TopLeft);

    private void PositionTopRight_Click(object sender, RoutedEventArgs e) => SetPosition(PositionChoice.TopRight);

    private void SetPosition(PositionChoice position)
    {
        model.Position = position;
        Log.Write($"posisi: {position}");
        SettingsChanged?.Invoke();
    }

    private void AccentOrange_Click(object sender, RoutedEventArgs e) => SetAccent(AccentChoice.Oranye);

    private void AccentBlue_Click(object sender, RoutedEventArgs e) => SetAccent(AccentChoice.Biru);

    private void AccentPurple_Click(object sender, RoutedEventArgs e) => SetAccent(AccentChoice.Ungu);

    private void AccentGreen_Click(object sender, RoutedEventArgs e) => SetAccent(AccentChoice.Hijau);

    private void SetAccent(AccentChoice accent)
    {
        model.Accent = accent;
        Log.Write($"aksen: {accent}");
        SettingsChanged?.Invoke();
    }

    private void LanguageId_Click(object sender, RoutedEventArgs e) => SetLanguage(DynamicIsland.Core.Language.Id);

    private void LanguageEn_Click(object sender, RoutedEventArgs e) => SetLanguage(DynamicIsland.Core.Language.En);

    private void SetLanguage(DynamicIsland.Core.Language language)
    {
        model.SetLanguage(language);
        Log.Write($"bahasa: {language}");
        SettingsChanged?.Invoke();
    }

    private void Context_Click(object sender, RoutedEventArgs e)
    {
        model.ContextEnabled = !model.ContextEnabled;
        Log.Write($"konteks: {(model.ContextEnabled ? "aktif" : "nonaktif")}");
        SettingsChanged?.Invoke();
    }

    private void SafeShare_Click(object sender, RoutedEventArgs e)
    {
        model.SafeShare = !model.SafeShare;
        Log.Write($"berbagi layar aman: {(model.SafeShare ? "aktif" : "nonaktif")}");
        SettingsChanged?.Invoke();
    }

    private void Study_Click(object sender, RoutedEventArgs e)
    {
        model.StudyMode = !model.StudyMode;
        Log.Write($"mode belajar: {(model.StudyMode ? "aktif" : "nonaktif")}");
        SettingsChanged?.Invoke();
    }

    private void Notifications_Click(object sender, RoutedEventArgs e)
    {
        model.NotificationsEnabled = !model.NotificationsEnabled;
        notifications.SetEnabled(model.NotificationsEnabled);
        Log.Write($"notifikasi windows: {(model.NotificationsEnabled ? "aktif" : "nonaktif")}");
        SettingsChanged?.Invoke();
    }

    private void Humor_Click(object sender, RoutedEventArgs e)
    {
        var next = model.Humor == HumorMode.Kocak ? HumorMode.Serius : HumorMode.Kocak;
        model.SetHumor(next);
        Log.Write($"mode kocak: {(next == HumorMode.Kocak ? "aktif" : "nonaktif")}");
        SettingsChanged?.Invoke();
    }

    private void PriorityDefault_Click(object sender, RoutedEventArgs e) => SetPriority(PriorityPreset.Bawaan);

    private void PriorityMedia_Click(object sender, RoutedEventArgs e) => SetPriority(PriorityPreset.MediaUtama);

    private void PriorityTimer_Click(object sender, RoutedEventArgs e) => SetPriority(PriorityPreset.TimerUtama);

    private void SetPriority(PriorityPreset preset)
    {
        model.SetPriority(preset);
        Log.Write($"prioritas: {preset}");
        SettingsChanged?.Invoke();
    }

    // --- Kontrol media di kartu expanded ---

    private void MediaPrevious_Click(object sender, RoutedEventArgs e)
    {
        controller.KeepExpanded();
        media.Previous();
        e.Handled = true;
    }

    private void MediaToggle_Click(object sender, RoutedEventArgs e)
    {
        controller.KeepExpanded();
        media.TogglePlayPause();
        e.Handled = true;
    }

    private void MediaNext_Click(object sender, RoutedEventArgs e)
    {
        controller.KeepExpanded();
        media.Next();
        e.Handled = true;
    }

    private void QuickActions_Click(object sender, RoutedEventArgs e) => model.ToggleQuickActions();

    private void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IslandViewModel.QuickAction action)
        {
            Log.Write($"aksi cepat: {action.Id}");

            switch (action.Id)
            {
                case "media-toggle": media.TogglePlayPause(); break;
                case "media-next": media.Next(); break;
                case "timer-focus": timer.StartFocus(); break;
                case "timer-stopwatch": timer.StartStopwatch(); break;
                case "screenshot": ScreenshotRequested?.Invoke(); break;
                case "camera": CameraRequested?.Invoke(); break;
                case "notes": NotesRequested?.Invoke(); break;
                case "clipboard": ClipboardRequested?.Invoke(); break;
            }

            model.CloseQuickActions();
        }

        e.Handled = true;
    }

    private void TimerFocus_Click(object sender, RoutedEventArgs e) => timer.StartFocus();

    private void TimerStopwatch_Click(object sender, RoutedEventArgs e) => timer.StartStopwatch();

    private void TimerStop_Click(object sender, RoutedEventArgs e) => timer.StopTimer();

    private void Shelf_Click(object sender, RoutedEventArgs e) => model.ToggleShelf();

    private void ShelfClear_Click(object sender, RoutedEventArgs e) => model.ClearShelf();

    /// <summary>Hanya menerima berkas saat shelf terbuka, supaya kapsul tidak merebut drag milik app lain.</summary>
    private void Capsule_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = model.IsShelfOpen && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void Capsule_Drop(object sender, DragEventArgs e)
    {
        if (model.IsShelfOpen && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var added = model.AddShelfFiles(paths);
            Log.Write($"shelf: {added} berkas ditambahkan");
        }

        e.Handled = true;
    }

    private void ShelfFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IslandViewModel.ShelfFile file)
        {
            try
            {
                Process.Start(new ProcessStartInfo(file.Path) { UseShellExecute = true });
                Log.Write($"shelf: membuka {file.Path}");
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            {
                Log.Write($"shelf: gagal membuka {file.Path}: {error.Message}");
            }
        }

        e.Handled = true;
    }

    /// <summary>Klik pertama membuka, klik berikutnya menutup.</summary>
    private void Toggle()
    {
        if (model.PrimaryState == ActivityState.Expanded)
        {
            controller.ClosePrimary();
        }
        else
        {
            controller.TapPrimary();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (model.IsHumorKocak && (model.HumorBehaviors?.EasterEgg ?? true))
        {
            var keyStr = e.Key.ToString();
            if (keyStr.Length == 1 && easterEgg.RegisterKey(keyStr[0], DateTimeOffset.UtcNow))
            {
                TriggerEasterEgg();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Escape when model.IsQuickActionsOpen:
                model.CloseQuickActions();
                e.Handled = true;
                break;
            case Key.Escape when model.IsShelfOpen:
                model.ToggleShelf();
                e.Handled = true;
                break;
            case Key.Escape when model.PrimaryState == ActivityState.Expanded:
                controller.ClosePrimary();
                e.Handled = true;
                break;
            case Key.Enter or Key.Space when model.HasActivity:
                Toggle();
                e.Handled = true;
                break;
        }
    }


    private void TriggerTickle()
    {
        Log.Write("humor: geli (5 klik cepat)");
        controller.Refresh(new DynamicIsland.Core.Activity(
            id: "humor-tickle",
            kind: DynamicIsland.Core.ActivityKind.Notification,
            title: model.Text.Get("humor.tickle"),
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddSeconds(3)));

        tickleWobbleStarted = DateTime.UtcNow;
        tickleWobbleTimer?.Stop();
        tickleWobbleTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            (_, _) =>
            {
                var elapsed = (DateTime.UtcNow - tickleWobbleStarted).TotalMilliseconds;
                if (elapsed >= 450)
                {
                    tickleWobbleTimer?.Stop();
                    Capsule.RenderTransform = null;
                    return;
                }

                var decay = 1.0 - (elapsed / 450.0);
                var offset = 6.0 * decay * Math.Sin(elapsed * 0.05);
                Capsule.RenderTransform = new TranslateTransform(offset, 0);
            },
            Dispatcher);
        tickleWobbleTimer.Start();
    }

    private void TriggerEasterEgg()
    {
        Log.Write("humor: telur paskah ditemukan!");
        controller.Refresh(new DynamicIsland.Core.Activity(
            id: "humor-easter",
            kind: DynamicIsland.Core.ActivityKind.Notification,
            title: model.Text.Get("humor.easter"),
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddSeconds(3)));
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ActivityAction action)
        {
            ActionInvoked?.Invoke(action);
        }

        e.Handled = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Log.Write("pengaturan: dibuka dari menu");
        SettingsRequested?.Invoke();
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }
}
