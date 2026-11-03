using System.Runtime.InteropServices;

namespace DynamicIsland.App;

/// <summary>
/// Daftar monitor beserta DPI efektifnya, hanya untuk log dan uji manual.
///
/// App ini PerMonitorV2 (lihat app.manifest): WPF memakai satuan DIP, jadi
/// kapsul 560 px di spesifikasi tampil 560 DIP dan Windows memperbesarnya
/// sesuai skala monitor. Log ini membuktikan angka DPI yang dibaca sistem,
/// bukan menebak dari resolusi.
/// </summary>
internal static class DisplayInfo
{
    private const uint MonitorEnumAll = 0;
    private const int MonitorInfoPrimary = 0x1;

    /// <summary>Menulis satu baris per monitor ke log.</summary>
    public static void LogMonitors()
    {
        try
        {
            var index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                index++;
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(monitor, ref info)) return true;

                var dpi = GetDpiForMonitor(monitor, DpiType.Effective, out var dpiX, out var dpiY) == 0
                    ? dpiX
                    : 96;

                var width = info.Monitor.Right - info.Monitor.Left;
                var height = info.Monitor.Bottom - info.Monitor.Top;
                var work = $"{info.Work.Right - info.Work.Left}x{info.Work.Bottom - info.Work.Top} di ({info.Work.Left},{info.Work.Top})";

                Log.Write(
                    $"monitor {index}: {width}x{height} di ({info.Monitor.Left},{info.Monitor.Top}), " +
                    $"area kerja {work}, dpi {dpiX}x{dpiY} ({Math.Round(dpiX / 96.0 * 100)}%)" +
                    (IsPrimary(info) ? ", primer" : string.Empty));
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Write("monitor: DPI per monitor tidak terbaca (" + error.Message + ")");
        }
    }

    private static bool IsPrimary(MonitorInfo info) => (info.Flags & MonitorInfoPrimary) != 0;

    private enum DpiType
    {
        Effective = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, DpiType type, out uint dpiX, out uint dpiY);
}
