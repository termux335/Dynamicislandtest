using System.IO;
using Microsoft.Win32;

namespace DynamicIsland.App;

/// <summary>
/// Mendaftarkan app di <c>HKCU\...\Run</c> supaya ikut jalan saat login.
///
/// Hanya kunci pengguna (tanpa hak admin) dan hanya satu nilai bernama
/// "DynamicIsland"; mencabutnya cukup menghapus nilai yang sama.
/// </summary>
internal static class StartupRegistration
{
    public const string ValueName = "DynamicIsland";

    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Path exe yang sedang berjalan, dipakai sebagai isi nilai Run.</summary>
    public static string ExecutablePath =>
        Environment.ProcessPath ?? throw new InvalidOperationException("path exe tidak diketahui");

    /// <summary>
    /// True hanya bila entri menunjuk ke exe yang sedang berjalan. Path lama
    /// (mis. setelah app dipindah) tidak dianggap aktif, supaya pilihan di
    /// pengaturan tidak berbohong.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value)) return false;

            return string.Equals(value.Trim('"'), ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Menyalakan atau mematikan autostart. False bila registry tidak bisa ditulis.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{ExecutablePath}\"", RegistryValueKind.String);
                Log.Write("autostart: didaftarkan di HKCU Run");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Write("autostart: dicabut dari HKCU Run");
            }

            return true;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Write("autostart: gagal ditulis (" + error.Message + ")");
            return false;
        }
    }

    /// <summary>
    /// Uji diri: balik status, baca ulang, lalu kembalikan seperti semula.
    /// Dipakai lewat <c>ISLAND_AUTOSTART_CHECK=1</c>; preferensi pengguna tidak berubah.
    /// </summary>
    public static void SelfCheck()
    {
        var before = IsEnabled();
        Log.Write("autostart: status awal " + (before ? "aktif" : "nonaktif"));

        var flipped = Set(!before);
        Log.Write($"autostart: tulis balik {(!before ? "aktif" : "nonaktif")} {(flipped ? "berhasil" : "gagal")}, " +
            "terbaca " + (IsEnabled() != before ? (before ? "nonaktif" : "aktif") : "tidak berubah"));

        Set(before);
        Log.Write("autostart: dipulihkan ke " + (IsEnabled() == before ? (before ? "aktif" : "nonaktif") : "status tak terduga"));
    }
}
