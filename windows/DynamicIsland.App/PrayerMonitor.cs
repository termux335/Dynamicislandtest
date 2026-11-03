using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Jadwal salat berikutnya (Tahap 4). Dihitung lokal dari koordinat; tidak ada
/// jaringan dan tidak ada izin baru.
/// </summary>
internal sealed class PrayerMonitor : IDisposable
{
    private readonly IslandController controller;
    private readonly IslandText text;
    private readonly DispatcherTimer timer;
    private DateOnly scheduleDate;
    private IReadOnlyList<PrayerTime> schedule = Array.Empty<PrayerTime>();
    private bool enabled;
    private bool shown;

    public PrayerMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.text = text;
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => Tick(),
            dispatcher);
    }

    public PrayerLocation Location { get; set; } = PrayerLocation.Jakarta;

    public bool Enabled => enabled;

    public void SetEnabled(bool value)
    {
        if (enabled == value) return;

        enabled = value;
        Log.Write(value ? $"salat: aktif di {Location.Latitude:0.###}, {Location.Longitude:0.###}" : "salat: dimatikan");

        if (value)
        {
            scheduleDate = default;
            Tick();
            timer.Start();
            return;
        }

        timer.Stop();
        shown = false;
        controller.Remove(PrayerPlan.ActivityId);
    }

    public void Start()
    {
        if (enabled)
        {
            Tick();
            timer.Start();
        }
    }

    public void Dispose() => timer.Stop();

    internal void Tick()
    {
        if (!enabled) return;

        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.LocalDateTime);

        if (scheduleDate != today)
        {
            schedule = PrayerCalculator.For(today, Location);
            scheduleDate = today;
        }

        var next = PrayerPlan.Next(schedule, now);
        if (next is null)
        {
            schedule = PrayerCalculator.For(today.AddDays(1), Location);
            scheduleDate = today.AddDays(1);
            next = PrayerPlan.Next(schedule, now);
        }

        if (next is null) return;

        if (!shown)
        {
            Log.Write($"salat: berikutnya {next.Value.Prayer} pukul {next.Value.At:HH:mm}");
            shown = true;
        }

        if (PrayerPlan.ActivityFor(next.Value, now, text) is not { } activity) return;

        controller.Refresh(activity);
    }
}
