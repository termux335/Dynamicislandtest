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

    /// <summary>Kontras tinggi memakai aksen putih murni; mematikan mengembalikan pilihan pengguna.</summary>
    public static void ApplyHighContrast(bool enabled, AccentChoice accent)
    {
        if (!enabled)
        {
            Apply(accent);
            return;
        }

        var brush = new SolidColorBrush(Colors.White);
        brush.Freeze();
        Application.Current.Resources[AccentBrushKey] = brush;
    }

    public static void Apply(AccentChoice accent) => ApplyRgb(accent.Rgb());

    /// <summary>Memakai warna dari sampul album, tetap aman untuk kontras gelap.</summary>
    public static void ApplyRgb(uint rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)(rgb >> 16),
            (byte)(rgb >> 8),
            (byte)rgb));

        brush.Freeze();
        Application.Current.Resources[AccentBrushKey] = brush;
    }
}
