using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Command palette (Tahap 5a): ketik perintah, hitungan, nama pintasan, atau isi
/// clipboard, lalu Enter untuk menjalankan.
/// </summary>
public partial class PaletteWindow : Window
{
    public PaletteWindow(string title, string placeholder, string hint, string empty)
    {
        InitializeComponent();

        TitleText.Text = title;
        Input.Text = string.Empty;
        HintText.Text = hint;
        EmptyText.Text = empty;

        Loaded += (_, _) => Input.Focus();
    }

    /// <summary>Pencarian disediakan App supaya urutan dan isinya bisa dites di Core.</summary>
    public Func<string, IReadOnlyList<PaletteEntry>>? Search { get; set; }

    public event Action<PaletteEntry>? Chosen;

    public void Refresh()
    {
        var entries = Search?.Invoke(Input.Text) ?? Array.Empty<PaletteEntry>();

        Results.ItemsSource = entries;
        if (entries.Count > 0) Results.SelectedIndex = 0;

        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Down:
                Move(1);
                e.Handled = true;
                break;

            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                Choose();
                e.Handled = true;
                break;
        }
    }

    private void Move(int delta)
    {
        var count = Results.Items.Count;
        if (count == 0) return;

        var next = Results.SelectedIndex + delta;
        Results.SelectedIndex = Math.Clamp(next, 0, count - 1);
        Results.ScrollIntoView(Results.SelectedItem);
    }

    private void Choose()
    {
        if (Results.SelectedItem is not PaletteEntry entry) return;

        Chosen?.Invoke(entry);
        Close();
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void Window_Deactivated(object sender, EventArgs e) => Close();
}
