using System.Runtime.InteropServices;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Memantau baterai lewat <c>GetSystemPowerStatus</c> (Win32, tanpa izin).
///
/// Yang diumumkan hanya transisi (lemah / mulai mengisi), lihat
/// <see cref="BatteryRules"/>; sampel pertama jadi acuan.
/// </summary>
internal sealed class BatteryMonitor : IDisposable
{
    private const byte UnknownPercent = 255;
    private const byte ChargingFlag = 8;

    private readonly IslandController controller;
    private readonly BatteryRules rules;
    private readonly DispatcherTimer timer;

    public BatteryMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        rules = new BatteryRules(text: text);
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => Sample(),
            dispatcher);
    }

    public void Start()
    {
        Sample();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    private void Sample()
    {
        if (!TryRead(out var state)) return;
        if (rules.OnSample(state) is not { } alert) return;

        Log.Write($"baterai: {alert.Title} ({alert.Subtitle})");
        controller.Refresh(alert);
    }

    /// <summary>Membaca daya sekarang. False bila persen tidak diketahui.</summary>
    internal static bool TryRead(out BatteryState state)
    {
        state = default;
        if (!GetSystemPowerStatus(out var status)) return false;
        if (status.BatteryLifePercent == UnknownPercent) return false;

        var charging = status.ACLineStatus == 1 || (status.BatteryFlag & ChargingFlag) != 0;
        state = new BatteryState(status.BatteryLifePercent, charging);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
