using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicIsland.App;

/// <summary>Pintasan global saat berada di dalam game (Tahap 4).</summary>
public enum IslandHotkey
{
    PlayPause,
    Next,
    MuteMic,
    Palette,
}

/// <summary>
/// Pintasan global Ctrl+Alt+Spasi (putar/jeda), Ctrl+Alt+N (lagu berikutnya),
/// dan Ctrl+Alt+M (bisukan mikrofon). Memakai <c>RegisterHotKey</c> pada window
/// kapsul; tidak ada keyboard hook.
/// </summary>
internal sealed class HotkeyHost
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private const int PlayPauseId = 0x5001;
    private const int NextId = 0x5002;
    private const int MuteId = 0x5003;
    private const int PaletteId = 0x5004;

    private const uint VkSpace = 0x20;
    private const uint VkN = 0x4E;
    private const uint VkM = 0x4D;
    private const uint VkV = 0x56;

    private readonly List<int> registered = new();
    private HwndSource? source;
    private bool detached;

    public HotkeyHost(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            Log.Write("pintasan: window belum punya handle; pintasan dilewati");
            return;
        }

        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);

        Register(handle, PlayPauseId, VkSpace, "play/jeda");
        Register(handle, NextId, VkN, "lagu berikutnya");
        Register(handle, MuteId, VkM, "bisukan mic");
        Register(handle, PaletteId, VkV, "perintah cepat");
    }

    public event Action<IslandHotkey>? Pressed;

    public int RegisteredCount => registered.Count;

    /// <summary>Melepas semua pintasan. Dipanggil sekali saat window ditutup.</summary>
    public void Detach()
    {
        if (detached) return;
        detached = true;

        var handle = source?.Handle ?? IntPtr.Zero;
        foreach (var id in registered)
        {
            if (handle != IntPtr.Zero) UnregisterHotKey(handle, id);
        }

        registered.Clear();
        source?.RemoveHook(WndProc);
        source = null;
    }

    private void Register(IntPtr handle, int id, uint key, string name)
    {
        if (!RegisterHotKey(handle, id, ModControl | ModAlt | ModNoRepeat, key))
        {
            Log.Write($"pintasan: Ctrl+Alt+{name} tidak bisa didaftarkan (mungkin sudah dipakai)");
            return;
        }

        registered.Add(id);
        Log.Write($"pintasan: Ctrl+Alt+{name} aktif");
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey) return IntPtr.Zero;

        var key = wParam.ToInt32() switch
        {
            PlayPauseId => IslandHotkey.PlayPause,
            NextId => IslandHotkey.Next,
            MuteId => IslandHotkey.MuteMic,
            PaletteId => IslandHotkey.Palette,
            _ => (IslandHotkey)(-1),
        };

        if ((int)key < 0) return IntPtr.Zero;

        handled = true;
        Pressed?.Invoke(key);
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
