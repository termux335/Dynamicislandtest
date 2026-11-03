using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DynamicIsland.Core;
using Microsoft.Win32;

namespace DynamicIsland.App;

/// <summary>
/// Memantau mikrofon lewat registry privasi Windows
/// (<c>CapabilityAccessManager\ConsentStore\microphone</c>).
///
/// Yang dibaca hanya nama aplikasi dan waktu mulai/berhenti pemakaian; isi
/// suara tidak pernah disentuh. Mikrofon dianggap aktif bila
/// <c>LastUsedTimeStop</c> bernilai 0. Tanpa izin tambahan dan tanpa hak admin.
/// </summary>
internal sealed class MicrophoneMonitor : IDisposable
{
    private const string ConsentRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private static readonly string[] Views = { "NonPackaged", "Packaged" };

    private const int SwRestore = 9;

    private readonly IslandController controller;
    private readonly IslandText text;
    private readonly DispatcherTimer timer;

    private bool shown;
    private bool muteProbed;

    public MicrophoneMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.text = text;
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(5),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    /// <summary>Status bisu mikrofon bawaan; false bila tidak diketahui.</summary>
    public bool IsMuted { get; private set; }

    /// <summary>Aplikasi yang sedang memakai mikrofon, untuk tombol "Kembali".</summary>
    public string? CurrentApp { get; private set; }

    public void Start()
    {
        if (!muteProbed)
        {
            muteProbed = true;

            if (MicrophoneMute.TryGetMuted(out var muted))
            {
                IsMuted = muted;
                Log.Write(muted ? "mic: mikrofon sedang dibisukan" : "mic: mikrofon aktif");
            }
            else
            {
                Log.Write("mic: status bisu tidak terbaca di perangkat ini");
            }
        }

        Poll();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    /// <summary>Membisukan atau menyalakan mikrofon, lalu memperbarui kapsul.</summary>
    public void ToggleMute()
    {
        var next = !IsMuted;

        if (!MicrophoneMute.TrySetMuted(next))
        {
            Log.Write("mic: bisu tidak bisa diubah di perangkat ini");
            return;
        }

        IsMuted = next;
        Log.Write(next ? "mic: dibisukan dari kapsul" : "mic: dinyalakan dari kapsul");
        Poll();
    }

    /// <summary>Membawa jendela aplikasi rapat ke depan. False bila tidak ditemukan.</summary>
    public bool BackToMeeting()
    {
        if (string.IsNullOrEmpty(CurrentApp))
        {
            Log.Write("rapat: tidak ada aplikasi rapat yang aktif");
            return false;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName(CurrentApp))
            {
                using (process)
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;

                    ShowWindow(process.MainWindowHandle, SwRestore);
                    if (SetForegroundWindow(process.MainWindowHandle))
                    {
                        Log.Write($"rapat: kembali ke {CurrentApp}");
                        return true;
                    }
                }
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            Log.Write("rapat: gagal membuka jendela (" + error.Message + ")");
            return false;
        }

        Log.Write($"rapat: jendela {CurrentApp} tidak ditemukan");
        return false;
    }

    private void Poll()
    {
        var users = ReadUsers();
        CurrentApp = users.Count == 0
            ? null
            : users.OrderBy(user => user.StartedAt).First().App;

        var activity = MeetingPlan.ActivityFor(users, DateTimeOffset.Now, IsMuted, text);

        if (activity is null)
        {
            if (!shown) return;

            shown = false;
            CurrentApp = null;
            controller.Remove(MeetingPlan.ActivityId);
            Log.Write("rapat: mikrofon tidak dipakai lagi");
            return;
        }

        shown = true;
        controller.Refresh(activity);
    }

    /// <summary>
    /// Pemakai mikrofon saat ini. Registry hanya menyimpan aplikasi yang pernah
    /// meminta akses; status "sedang dipakai" ditandai <c>LastUsedTimeStop</c> = 0.
    /// </summary>
    internal static IReadOnlyList<MicrophoneUser> ReadUsers()
    {
        var users = new List<MicrophoneUser>();

        foreach (var view in Views)
        {
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(ConsentRoot + "\\" + view);
                if (root is null) continue;

                foreach (var key in root.GetSubKeyNames())
                {
                    using var app = root.OpenSubKey(key);
                    if (app is null) continue;

                    if (ReadFileTime(app, "LastUsedTimeStart") is not { } startedAt || startedAt <= 0) continue;
                    if (ReadFileTime(app, "LastUsedTimeStop") is not { } stoppedAt || stoppedAt != 0) continue;

                    users.Add(new MicrophoneUser(
                        MeetingPlan.AppNameFromConsentKey(key),
                        DateTimeOffset.FromFileTime(startedAt)));
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Tidak boleh membaca registry: anggap tidak ada rapat.
            }
        }

        return users;
    }

    /// <summary>Nilai FILETIME mentah; 0 berarti mikrofon sedang tidak dipakai.</summary>
    private static long? ReadFileTime(RegistryKey key, string name) => key.GetValue(name) switch
    {
        long number => number,
        int number => number,
        _ => null,
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
