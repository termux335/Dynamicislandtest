using System.Runtime.InteropServices;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Menampilkan volume dan kecerahan sebagai kapsul (Tahap 5a).
///
/// OSD bawaan Windows tetap ada karena tidak bisa dimatikan tanpa hook; modul ini
/// adalah tambahan dan punya saklar sendiri. Volume dibaca lewat Core Audio,
/// kecerahan lewat DDC/CI. Bila perangkat tidak mendukung, modul diam dan
/// alasannya dicatat sekali.
/// </summary>
internal sealed class OsdMonitor : IDisposable
{
    private static bool brightnessUnavailable;

    private readonly IslandController controller;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;
    private bool enabled;
    private int? lastVolume;
    private bool? lastMute;
    private int? lastBrightness;

    public OsdMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.text = text;

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    public void SetEnabled(bool value)
    {
        if (enabled == value) return;

        enabled = value;
        Log.Write(value ? "osd: tampilan volume/kecerahan aktif" : "osd: tampilan volume/kecerahan dimatikan");

        if (value)
        {
            Poll();
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    public void Dispose() => timer.Stop();

    private void Poll()
    {
        if (!enabled) return;

        var now = DateTimeOffset.Now;

        if (TryReadVolume(out var volume, out var mute))
        {
            if (lastVolume is null)
            {
                lastVolume = volume;
                lastMute = mute;
            }
            else if (lastMute != mute)
            {
                lastMute = mute;
                lastVolume = volume;
                Publish(new OsdState(OsdKind.Mute, volume, mute), now);
                return;
            }
            else if (lastVolume != volume)
            {
                lastVolume = volume;
                Publish(new OsdState(OsdKind.Volume, volume), now);
                return;
            }
        }

        if (!TryReadBrightness(out var brightness)) return;

        if (lastBrightness is null)
        {
            lastBrightness = brightness;
            return;
        }

        if (lastBrightness == brightness) return;

        lastBrightness = brightness;
        Publish(new OsdState(OsdKind.Brightness, brightness), now);
    }

    private void Publish(OsdState state, DateTimeOffset now)
    {
        controller.Refresh(Osd.ActivityFor(state, now, text));
    }

    // --- Volume lewat Core Audio ---

    private static bool TryReadVolume(out int percent, out bool mute)
    {
        percent = 0;
        mute = false;

        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? endpoint = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();

            if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device is null) return false;

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, 23, IntPtr.Zero, out var instance) != 0) return false;

            endpoint = (IAudioEndpointVolume)instance;
            if (endpoint.GetMasterVolumeLevelScalar(out var level) != 0) return false;
            endpoint.GetMute(out mute);

            percent = (int)Math.Round(Math.Clamp(level, 0, 1) * 100);
            return true;
        }
        catch (Exception error) when (error is COMException or InvalidCastException)
        {
            return false;
        }
        finally
        {
            if (endpoint is not null) Marshal.ReleaseComObject(endpoint);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    // --- Kecerahan lewat DDC/CI (dxva2) ---

    private static bool TryReadBrightness(out int percent)
    {
        percent = 0;

        if (brightnessUnavailable) return false;

        var monitor = MonitorFromWindow(IntPtr.Zero, MonitorDefaultToPrimary);
        if (monitor == IntPtr.Zero) return false;

        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0) return false;

        var monitors = new PhysicalMonitor[count];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, monitors)) return false;

        try
        {
            foreach (var physical in monitors)
            {
                if (!GetMonitorBrightness(physical.Handle, out _, out var current, out var maximum) || maximum == 0) continue;

                percent = (int)Math.Round(current * 100.0 / maximum);
                return true;
            }
        }
        finally
        {
            DestroyPhysicalMonitors(count, monitors);
        }

        brightnessUnavailable = true;
        Log.Write("osd: kecerahan tidak didukung monitor ini; hanya volume yang ditampilkan");
        return false;
    }

    private const int MonitorDefaultToPrimary = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] monitors);

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice(string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr callback);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid id, int clsContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

        [PreserveSig]
        int OpenPropertyStore(int access, out IntPtr store);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out int state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid context);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid context);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, ref Guid context);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
