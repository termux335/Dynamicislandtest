using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DynamicIsland.App;

public sealed record ScreenshotResult(bool Ok, string? Path, string? Error);

/// <summary>
/// Tangkapan layar (Tahap 5c.15). Menyimpan PNG ke folder Pictures pengguna;
/// tidak mengunggah apa pun. Rekaman layar belum termasuk di versi ini.
/// </summary>
public static class ScreenshotService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public static string Folder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "DynamicIsland");

    public static ScreenshotResult Capture()
    {
        try
        {
            var x = GetSystemMetrics(SmXVirtualScreen);
            var y = GetSystemMetrics(SmYVirtualScreen);
            var width = GetSystemMetrics(SmCxVirtualScreen);
            var height = GetSystemMetrics(SmCyVirtualScreen);

            if (width <= 0 || height <= 0) return new ScreenshotResult(false, null, "Ukuran layar tidak terbaca");

            Directory.CreateDirectory(Folder);
            var path = System.IO.Path.Combine(Folder, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            using var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            }

            bitmap.Save(path, ImageFormat.Png);
            Log.Write($"screenshot: {width}x{height} disimpan ke {path}");

            return new ScreenshotResult(true, path, null);
        }
        catch (Exception error) when (error is ExternalException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Log.Write("screenshot: gagal (" + error.Message + ")");
            return new ScreenshotResult(false, null, error.Message);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
