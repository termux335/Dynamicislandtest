using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DynamicIsland.App;

/// <summary>
/// Waktu sejak input terakhir, untuk pelindung layar OLED (Tahap 4).
///
/// Hanya membaca waktu input terakhir (<c>GetLastInputInfo</c>); tidak pernah
/// merekam tombol, teks, atau isi ketikan.
/// </summary>
internal sealed class IdleWatcher : IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly DateTimeOffset startedAt = DateTimeOffset.Now;

    public IdleWatcher(Dispatcher dispatcher)
    {
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Sample(),
            dispatcher);
    }

    public TimeSpan Idle { get; private set; }

    public TimeSpan Uptime => DateTimeOffset.Now - startedAt;

    public void Start()
    {
        Sample();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    private void Sample()
    {
        Idle = ReadIdle();
    }

    internal static TimeSpan ReadIdle()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;

        var now = (uint)GetTickCount64();
        var elapsed = unchecked(now - info.Tick);

        // Tick 32-bit bisa berputar; nilai lebih dari 24 hari dianggap baru saja.
        return elapsed > (uint)TimeSpan.FromDays(1).TotalMilliseconds
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(elapsed);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Tick;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
