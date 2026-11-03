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
        AutostartCheck.IsChecked = model.StartWithWindows;

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
        if (ready) model.SetHumor(HumorCheck.IsChecked == true ? HumorMode.Kocak : HumorMode.Serius);
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

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Saved?.Invoke();
        Close();
    }
}
