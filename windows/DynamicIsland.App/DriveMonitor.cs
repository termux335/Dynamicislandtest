using System.IO;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Memantau volume/berkas terpasang (USB, kartu SD) sebagai "perangkat terhubung".
///
/// Bluetooth di Windows butuh WinRT dan izin khusus app model, jadi untuk laptop
/// sumbernya volume yang bisa dilepas. Hanya perubahan daftar yang diumumkan,
/// dan daftar awal tidak diumumkan (Tahap 2).
/// </summary>
internal sealed class DriveMonitor : IDisposable
{
    private readonly IslandController controller;
    private readonly DeviceRules rules;
    private readonly DispatcherTimer timer;

    public DriveMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        rules = new DeviceRules(announceFirstSample: false, text: text);
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(20),
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
        if (rules.OnSample(ReadNames()) is not { } alert) return;

        Log.Write($"perangkat: {alert.Title} ({alert.Subtitle})");
        controller.Refresh(alert);
    }

    /// <summary>Label volume yang bisa dilepas; nama drive dipakai bila tanpa label.</summary>
    internal static IReadOnlyList<string> ReadNames()
    {
        var names = new List<string>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;

                    var label = drive.VolumeLabel;
                    names.Add(string.IsNullOrWhiteSpace(label) ? drive.Name.TrimEnd('\\') : label);
                }
                catch (IOException)
                {
                    // Drive dicabut saat diperiksa.
                }
                catch (UnauthorizedAccessException)
                {
                    // Volume tanpa akses tetap dihitung sebagai terpasang, tapi tanpa nama.
                    names.Add(drive.Name.TrimEnd('\\'));
                }
            }
        }
        catch (IOException)
        {
            // Gagal membaca daftar drive: anggap tidak ada perubahan.
        }

        return names;
    }
}
