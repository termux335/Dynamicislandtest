using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Memantau aplikasi yang sedang di depan tiap detik untuk aturan penempatan:
/// nama proses dan kotak window saja. Tidak ada hook keyboard/mouse, tidak ada
/// injeksi, dan tidak ada izin tambahan.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>Batas toleransi kotak window untuk maximized (Windows menambah border tak terlihat).</summary>
    private const int MaximizedTolerance = 14;

    /// <summary>Batas toleransi untuk layar penuh sejati.</summary>
    private const int FullscreenTolerance = 2;

    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "chromium", "arc",
    };

    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "TextInputHost",
    };

    private readonly DispatcherTimer timer;
    private bool reported;

    public ForegroundWatcher(Dispatcher dispatcher)
    {
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    /// <summary>Info window depan terakhir, atau null bila tidak terbaca.</summary>
    public ForegroundInfo? Current { get; private set; }

    /// <summary>Hanya dipanggil saat kategori atau bentuk window berubah.</summary>
    public event Action<ForegroundInfo?>? Changed;

    public void Start()
    {
        Poll();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    private void Poll()
    {
        var next = Read();

        if (reported && next == Current) return;

        reported = true;
        Current = next;
        Changed?.Invoke(next);
    }

    private static ForegroundInfo? Read()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;

        // Nilai balik 0 berarti pemanggilan gagal; jangan pakai processId yang tidak sah.
        var threadId = GetWindowThreadProcessId(window, out var processId);
        if (threadId == 0 || processId == 0 || processId == Environment.ProcessId) return null;

        var process = ProcessName(processId);
        if (process is null) return null;

        if (!GetWindowRect(window, out var rect)) return null;

        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return null;

        var fullscreen = Covers(rect, info.Monitor, FullscreenTolerance);
        var maximized = !fullscreen && Covers(rect, info.Work, MaximizedTolerance);

        return new ForegroundInfo(Categorize(process, fullscreen), fullscreen, maximized, process);
    }

    private static string? ProcessName(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private static ForegroundCategory Categorize(string process, bool fullscreen)
    {
        if (Browsers.Contains(process)) return ForegroundCategory.Browser;
        if (ShellProcesses.Contains(process)) return ForegroundCategory.Other;
        return fullscreen ? ForegroundCategory.Game : ForegroundCategory.Other;
    }

    /// <summary>True bila kotak window menutup seluruh kotak pembanding.</summary>
    private static bool Covers(Rect window, Rect area, int tolerance) =>
        window.Left <= area.Left + tolerance &&
        window.Top <= area.Top + tolerance &&
        window.Right >= area.Right - tolerance &&
        window.Bottom >= area.Bottom - tolerance;

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
