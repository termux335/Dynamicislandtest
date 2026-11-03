using System.Windows;
using System.Windows.Media;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Menyalin pilihan aksen ke satu brush aplikasi. XAML memakai
/// <c>DynamicResource AccentBrush</c>, jadi pergantian warna langsung terlihat
/// di semua tempat termasuk di dalam template.
/// </summary>
internal static class Theme
{
    public const string AccentBrushKey = "AccentBrush";

    public static void Apply(AccentChoice accent)
    {
        var rgb = accent.Rgb();
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)(rgb >> 16),
            (byte)(rgb >> 8),
            (byte)rgb));

        brush.Freeze();
        Application.Current.Resources[AccentBrushKey] = brush;
    }
}
