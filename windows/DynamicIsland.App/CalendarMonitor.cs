using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Membaca acara berikutnya dari berkas <c>.ics</c> lokal (Tahap 2).
///
/// Path diambil dari variabel lingkungan <c>ISLAND_ICS</c>; tanpa itu dipakai
/// <c>%TEMP%\dynamic-island.ics</c> supaya skrip uji bisa menaruh berkasnya.
/// Tidak ada jaringan dan tidak ada izin: berkas dibaca lokal.
/// </summary>
internal sealed class CalendarMonitor : IDisposable
{
    public const string EnvironmentVariable = "ISLAND_ICS";

    private const string DefaultFileName = "dynamic-island.ics";

    private readonly IslandController controller;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;
    private bool shown;
    private string? lastTitle;

    public CalendarMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        this.text = text ?? IslandText.Default;
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    /// <summary>Tautan pertemuan acara yang sedang tampil, bila ada.</summary>
    public string? CurrentMeetingUrl { get; private set; }

    /// <summary>Berkas yang dibaca sekarang.</summary>
    public static string SourcePath =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } custom
            ? custom
            : Path.Combine(Path.GetTempPath(), DefaultFileName);

    public void Start()
    {
        Poll();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    private void Poll()
    {
        var next = CalendarPlan.Next(ReadEvents(), DateTimeOffset.Now);

        if (next is null)
        {
            CurrentMeetingUrl = null;
            if (!shown) return;

            controller.Remove(CalendarPlan.Id);
            shown = false;
            lastTitle = null;
            Log.Write("kalender: tidak ada acara berikutnya");
            return;
        }

        CurrentMeetingUrl = next.MeetingUrl;
        var activity = CalendarPlan.ActivityFor(next, DateTimeOffset.Now, text);

        if (!shown || lastTitle != next.Title)
        {
            Log.Write($"kalender: {next.Title} ({activity.Subtitle})");
            lastTitle = next.Title;
        }

        shown = true;
        controller.Refresh(activity);
    }

    private static IReadOnlyList<CalendarEvent> ReadEvents()
    {
        var path = SourcePath;

        try
        {
            return File.Exists(path) ? IcsCalendar.Parse(File.ReadAllText(path)) : Array.Empty<CalendarEvent>();
        }
        catch (IOException error)
        {
            Log.Write($"kalender: gagal membaca {path}: {error.Message}");
            return Array.Empty<CalendarEvent>();
        }
        catch (UnauthorizedAccessException error)
        {
            Log.Write($"kalender: tidak boleh membaca {path}: {error.Message}");
            return Array.Empty<CalendarEvent>();
        }
    }

    /// <summary>Membuka tautan pertemuan acara yang tampil (tombol "Gabung").</summary>
    public bool OpenMeeting()
    {
        if (string.IsNullOrEmpty(CurrentMeetingUrl))
        {
            Log.Write("kalender: tidak ada tautan pertemuan untuk acara ini");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(CurrentMeetingUrl) { UseShellExecute = true });
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Write($"kalender: gagal membuka tautan: {error.Message}");
            return false;
        }
    }
}
