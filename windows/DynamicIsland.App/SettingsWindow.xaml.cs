using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Jendela onboarding (pertama kali jalan) dan pengaturan.
///
/// Semua pilihan diterapkan langsung ke <see cref="IslandViewModel"/> supaya
/// kapsul ikut berubah saat itu juga; penyimpanan ke berkas dilakukan App saat
/// jendela ditutup, lewat event <see cref="Saved"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IslandViewModel model;
    private readonly bool firstRun;

    /// <summary>Menahan handler selama kontrol diisi, supaya tidak menimpa pilihan tersimpan.</summary>
    private bool ready;

    public SettingsWindow(IslandViewModel model, bool firstRun)
    {
        InitializeComponent();

        this.model = model;
        this.firstRun = firstRun;
        DataContext = model;

        Title = firstRun ? model.LabelWelcomeTitle : model.LabelSettingsTitle;

        // Lewat binding, bukan teks tetap: mengganti bahasa di jendela ini
        // langsung mengubah judul dan tombolnya juga.
        Headline.SetBinding(TextBlock.TextProperty, new Binding(firstRun
            ? nameof(IslandViewModel.LabelWelcomeTitle)
            : nameof(IslandViewModel.LabelSettingsTitle)));
        PrimaryButton.SetBinding(ContentControl.ContentProperty, new Binding(firstRun
            ? nameof(IslandViewModel.LabelStart)
            : nameof(IslandViewModel.LabelClose)));

        LanguageId.IsChecked = model.IsLanguageId;
        LanguageEn.IsChecked = model.IsLanguageEn;
        PositionBox.SelectedIndex = (int)model.Position;
        AccentOrange.IsChecked = model.IsAccentOranye;
        AccentBlue.IsChecked = model.IsAccentBiru;
        AccentPurple.IsChecked = model.IsAccentUngu;
        AccentGreen.IsChecked = model.IsAccentHijau;
        HumorCheck.IsChecked = model.Humor == HumorMode.Kocak;
        HumorDetailsPanel.Visibility = model.Humor == HumorMode.Kocak ? Visibility.Visible : Visibility.Collapsed;
        var hb = model.HumorBehaviors;
        HumorEyesCheck.IsChecked = hb.Eyes;
        HumorTickleCheck.IsChecked = hb.Tickle;
        HumorSulkCheck.IsChecked = hb.Sulking;
        HumorRepeatCheck.IsChecked = hb.RepeatTrack;
        HumorDramaCheck.IsChecked = hb.DramaticCountdown;
        HumorEasterCheck.IsChecked = hb.EasterEgg;
        AutostartCheck.IsChecked = model.StartWithWindows;
        ContextCheck.IsChecked = model.ContextEnabled;
        SafeShareCheck.IsChecked = model.SafeShare;
        HabitsCheck.IsChecked = model.HabitsEnabled;
        PrayerCheck.IsChecked = model.PrayerEnabled;
        OledCheck.IsChecked = model.OledProtection;
        WeatherCheck.IsChecked = model.WeatherEnabled;
        ClipboardCheck.IsChecked = model.ClipboardEnabled;
        PaletteCheck.IsChecked = model.PaletteEnabled;
        OsdCheck.IsChecked = model.OsdEnabled;
        MirrorCheck.IsChecked = model.MirrorEnabled;
        NotesCheck.IsChecked = model.NotesEnabled;
        TypingCheck.IsChecked = model.TypingDeferral;
        QuietCheck.IsChecked = model.QuietHoursEnabled;
        FillHours(QuietStartBox, model.QuietStartHour);
        FillHours(QuietEndBox, model.QuietEndHour);
        FocusMusicBox.Text = model.FocusMusic;
        AlbumColorCheck.IsChecked = model.AlbumColor;
        VisualizerCheck.IsChecked = model.Visualizer;
        VoiceCheck.IsChecked = model.VoiceNotes;
        SpeakCheck.IsChecked = model.SpeakNotifications;
        LargeTextCheck.IsChecked = model.LargeText;
        ContrastCheck.IsChecked = model.HighContrast;
        AgentStatusCheck.IsChecked = model.AgentStatusEnabled;
        TerminalCheck.IsChecked = model.TerminalEnabled;
        ScreenshotCheck.IsChecked = model.ScreenshotEnabled;
        TickerCheck.IsChecked = model.TickerEnabled;
        TickerUrlBox.Text = model.TickerUrl;
        AssistantCheck.IsChecked = model.AssistantEnabled;
        PluginsCheck.IsChecked = model.PluginsEnabled;

        ready = true;
    }

    /// <summary>Dipanggil setelah pengguna menutup jendela (untuk menyimpan pengaturan).</summary>
    public event Action? Saved;

    public bool IsFirstRun => firstRun;

    /// <summary>Menulis entri autostart; false bila registry menolak.</summary>
    public Func<bool, bool>? ApplyAutostart { get; init; }

    private void LanguageId_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.SetLanguage(DynamicIsland.Core.Language.Id);
    }

    private void LanguageEn_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.SetLanguage(DynamicIsland.Core.Language.En);
    }

    private void Position_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || PositionBox.SelectedIndex < 0) return;

        model.Position = (PositionChoice)PositionBox.SelectedIndex;
    }

    private void AccentOrange_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.Accent = AccentChoice.Oranye;
    }

    private void AccentBlue_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.Accent = AccentChoice.Biru;
    }

    private void AccentPurple_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.Accent = AccentChoice.Ungu;
    }

    private void AccentGreen_Checked(object sender, RoutedEventArgs e)
    {
        if (ready) model.Accent = AccentChoice.Hijau;
    }

    private void Humor_Changed(object sender, RoutedEventArgs e)
    {
        if (ready)
        {
            var kocak = HumorCheck.IsChecked == true;
            model.SetHumor(kocak ? HumorMode.Kocak : HumorMode.Serius);
            HumorDetailsPanel.Visibility = kocak ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void HumorBehavior_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        model.HumorBehaviors = new HumorSettings(
            Eyes: HumorEyesCheck.IsChecked == true,
            Tickle: HumorTickleCheck.IsChecked == true,
            Sulking: HumorSulkCheck.IsChecked == true,
            RepeatTrack: HumorRepeatCheck.IsChecked == true,
            DramaticCountdown: HumorDramaCheck.IsChecked == true,
            EasterEgg: HumorEasterCheck.IsChecked == true);
    }

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        var wanted = AutostartCheck.IsChecked == true;

        // Gagal menulis registry tidak boleh diam: kotak centang dikembalikan dan
        // sebabnya ditampilkan di bawahnya.
        if (ApplyAutostart is { } apply && !apply(wanted))
        {
            ready = false;
            AutostartCheck.IsChecked = !wanted;
            ready = true;
            model.StartWithWindows = !wanted;
            AutostartStatus.Text = model.LabelAutostartFailed;
            AutostartStatus.Visibility = Visibility.Visible;
            return;
        }

        AutostartStatus.Visibility = Visibility.Collapsed;
        model.StartWithWindows = wanted;
    }

    private void Context_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.ContextEnabled = ContextCheck.IsChecked == true;
    }

    private void SafeShare_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.SafeShare = SafeShareCheck.IsChecked == true;
    }

    private void Habits_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.HabitsEnabled = HabitsCheck.IsChecked == true;
    }

    private void Prayer_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.PrayerEnabled = PrayerCheck.IsChecked == true;
    }

    private void Oled_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.OledProtection = OledCheck.IsChecked == true;
    }

    /// <summary>
    /// Cuaca memakai internet, jadi izinnya diminta saat dinyalakan. Menolak
    /// mengembalikan kotak centang dan tidak pernah mengaktifkan modul.
    /// </summary>
    private void Weather_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        var wanted = WeatherCheck.IsChecked == true;

        if (wanted)
        {
            var answer = MessageBox.Show(
                this,
                model.LabelWeatherNote,
                model.LabelWeather,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                WeatherCheck.IsChecked = false;
                ready = true;
                model.WeatherEnabled = false;
                return;
            }
        }

        model.WeatherEnabled = wanted;
    }

    private void Clipboard_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.ClipboardEnabled = ClipboardCheck.IsChecked == true;
    }

    private void Palette_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.PaletteEnabled = PaletteCheck.IsChecked == true;
    }

    private void Osd_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.OsdEnabled = OsdCheck.IsChecked == true;
    }

    /// <summary>Kamera memakai izin Windows; penjelasannya ditampilkan saat dinyalakan.</summary>
    private void Mirror_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        var wanted = MirrorCheck.IsChecked == true;

        if (wanted)
        {
            var answer = MessageBox.Show(
                this,
                model.LabelMirrorNote,
                model.LabelMirror,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                MirrorCheck.IsChecked = false;
                ready = true;
                model.MirrorEnabled = false;
                return;
            }
        }

        model.MirrorEnabled = wanted;
    }

    private void Notes_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.NotesEnabled = NotesCheck.IsChecked == true;
    }

    private void Typing_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.TypingDeferral = TypingCheck.IsChecked == true;
    }

    private void Quiet_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.QuietHoursEnabled = QuietCheck.IsChecked == true;
    }

    private void QuietStart_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ready && QuietStartBox.SelectedIndex >= 0) model.QuietStartHour = QuietStartBox.SelectedIndex;
    }

    private void QuietEnd_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ready && QuietEndBox.SelectedIndex >= 0) model.QuietEndHour = QuietEndBox.SelectedIndex;
    }

    private void FocusMusic_Changed(object sender, TextChangedEventArgs e)
    {
        if (ready) model.FocusMusic = FocusMusicBox.Text;
    }

    private void FocusMusicBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = model.LabelFocusMusic,
            Filter = "Audio/playlist|*.m3u;*.m3u8;*.mp3;*.wav;*.flac;*.aac;*.ogg;*.wma|Semua berkas|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        // TextChanged yang menyalin ke model; handler tetap jalan karena ready.
        FocusMusicBox.Text = dialog.FileName;
    }

    private void AlbumColor_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.AlbumColor = AlbumColorCheck.IsChecked == true;
    }

    private void Visualizer_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.Visualizer = VisualizerCheck.IsChecked == true;
    }

    private static void FillHours(ComboBox box, int selected)
    {
        for (var hour = 0; hour < 24; hour++) box.Items.Add($"{hour:00}:00");

        box.SelectedIndex = Math.Clamp(selected, 0, 23);
    }

    private void Voice_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.VoiceNotes = VoiceCheck.IsChecked == true;
    }

    private void Speak_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.SpeakNotifications = SpeakCheck.IsChecked == true;
    }

    private void LargeText_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.LargeText = LargeTextCheck.IsChecked == true;
    }

    private void Contrast_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.HighContrast = ContrastCheck.IsChecked == true;
    }

    private void AgentStatus_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.AgentStatusEnabled = AgentStatusCheck.IsChecked == true;
    }

    private void ClaudeHook_Click(object sender, RoutedEventArgs e)
    {
        if (ClaudeHookManager.IsInstalled())
        {
            var (success, msg) = ClaudeHookManager.Uninstall();
            if (success)
            {
                model.RefreshClaudeHookStatus();
            }
            else
            {
                MessageBox.Show(this, msg, "Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            ClaudeHookManager.DeployHookScripts();
            var (success, msg) = ClaudeHookManager.Install();
            if (success)
            {
                model.RefreshClaudeHookStatus();
            }
            else
            {
                MessageBox.Show(this, msg, "Dynamic Island", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };

    private void ClaudeHookTest_Click(object sender, RoutedEventArgs e)
    {
        var dir = ClaudeHookManager.DefaultHooksDirectory;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var statusPath = Path.Combine(dir, "agent-status.json");
        var reqId = "test-" + Guid.NewGuid().ToString("N")[..6];
        var testStatus = new
        {
            tool = "Claude Code",
            state = "waiting",
            request = new
            {
                id = reqId,
                summary = "Bash: git push origin main",
                detail = "git push origin main"
            },
            updated_at = DateTimeOffset.Now.ToString("o")
        };

        File.WriteAllText(statusPath, JsonSerializer.Serialize(testStatus, JsonWriteOptions));
    }

    private void Terminal_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        if (TerminalCheck.IsChecked == true)
        {
            var answer = MessageBox.Show(this, model.LabelTerminalNote, model.LabelTerminal,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                TerminalCheck.IsChecked = false;
                ready = true;
                model.TerminalEnabled = false;
                return;
            }
        }

        model.TerminalEnabled = TerminalCheck.IsChecked == true;
    }

    private void Screenshot_Changed(object sender, RoutedEventArgs e)
    {
        if (ready) model.ScreenshotEnabled = ScreenshotCheck.IsChecked == true;
    }

    /// <summary>Ticker memakai internet: izin diminta saat dinyalakan.</summary>
    private void Ticker_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        if (TickerCheck.IsChecked == true)
        {
            var answer = MessageBox.Show(this, model.LabelTickerNote, model.LabelTicker,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                TickerCheck.IsChecked = false;
                ready = true;
                model.TickerEnabled = false;
                return;
            }
        }

        model.TickerEnabled = TickerCheck.IsChecked == true;
    }

    private void TickerUrl_Changed(object sender, TextChangedEventArgs e)
    {
        if (ready) model.TickerUrl = TickerUrlBox.Text;
    }

    /// <summary>Asisten mengirim data ke endpoint pilihan pengguna saat Kirim ditekan.</summary>
    private void Assistant_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        if (AssistantCheck.IsChecked == true)
        {
            var answer = MessageBox.Show(this, model.LabelAssistantNote, model.LabelAssistant,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                AssistantCheck.IsChecked = false;
                ready = true;
                model.AssistantEnabled = false;
                return;
            }
        }

        model.AssistantEnabled = AssistantCheck.IsChecked == true;
    }

    private void Plugins_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;

        if (PluginsCheck.IsChecked == true)
        {
            var answer = MessageBox.Show(this, model.LabelPluginsNote, model.LabelPlugins,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                ready = false;
                PluginsCheck.IsChecked = false;
                ready = true;
                model.PluginsEnabled = false;
                return;
            }
        }

        model.PluginsEnabled = PluginsCheck.IsChecked == true;
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Saved?.Invoke();
        Close();
    }
}
