using System.Runtime.InteropServices;

namespace DynamicIsland.App;

/// <summary>
/// Membisukan mikrofon bawaan lewat Core Audio (IAudioEndpointVolume).
///
/// Hanya mikrofon (data flow capture) milik perangkat output bawaan yang
/// disentuh; app lain tidak diubah. Bila perangkat atau API-nya tidak ada,
/// pemanggil diberi tahu (false) supaya tombol bisa melaporkan sebabnya.
/// </summary>
internal static class MicrophoneMute
{
    private const int CaptureFlow = 1;
    private const int ConsoleRole = 0;
    private const uint ClsCtxAll = 23;

    public static bool TryGetMuted(out bool muted)
    {
        muted = false;

        IAudioEndpointVolume? volume = null;

        try
        {
            volume = OpenDefaultCapture();
            if (volume is null) return false;

            ThrowIfFailed(volume.GetMute(out var value));
            muted = value != 0;
            return true;
        }
        catch (Exception error) when (error is COMException or InvalidCastException or ArgumentException)
        {
            Log.Write("mic: status bisu tidak terbaca (" + error.Message + ")");
            return false;
        }
        finally
        {
            if (volume is not null) Marshal.ReleaseComObject(volume);
        }
    }

    public static bool TrySetMuted(bool muted)
    {
        IAudioEndpointVolume? volume = null;

        try
        {
            volume = OpenDefaultCapture();
            if (volume is null) return false;

            ThrowIfFailed(volume.SetMute(muted ? 1 : 0, Guid.Empty));
            return true;
        }
        catch (Exception error) when (error is COMException or InvalidCastException or ArgumentException)
        {
            Log.Write("mic: gagal mengubah bisu (" + error.Message + ")");
            return false;
        }
        finally
        {
            if (volume is not null) Marshal.ReleaseComObject(volume);
        }
    }

    private static IAudioEndpointVolume? OpenDefaultCapture()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(CaptureFlow, ConsoleRole, out device));

            var iid = typeof(IAudioEndpointVolume).GUID;
            ThrowIfFailed(device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var pointer));
            if (pointer == IntPtr.Zero) return null;

            try
            {
                return (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(pointer);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }
        catch (COMException error) when (error.HResult == unchecked((int)0x80070490) || error.HResult == unchecked((int)0x8007000E))
        {
            // Tidak ada perangkat mikrofon.
            Log.Write("mic: tidak ada mikrofon bawaan");
            return null;
        }
        finally
        {
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0) Marshal.ThrowExceptionForHR(result);
    }

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
        int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr collection);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParams, out IntPtr instance);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr store);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    /// <summary>Urutan metode harus sama dengan vtable asli; SetMute/GetMute ada di tengah.</summary>
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
        int SetMasterVolumeLevel(float level, Guid context);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, Guid context);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, Guid context);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, Guid context);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute(int muted, Guid context);

        [PreserveSig]
        int GetMute(out int muted);
    }
}
