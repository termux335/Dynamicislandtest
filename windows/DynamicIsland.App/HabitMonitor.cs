using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Pengingat kebiasaan (Tahap 4). Menghitung waktu saja; tidak membaca aktivitas
/// pengguna dan tidak pernah merekam ketikan.
/// </summary>
internal sealed class HabitMonitor : IDisposable
{
    private readonly IslandController controller;
    private readonly IslandText text;
    private readonly HabitCoach coach;
    private readonly DispatcherTimer timer;
    private bool enabled;

    public HabitMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.text = text;
        coach = new HabitCoach(new HabitSettings(), text: text);

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) => Tick(),
            dispatcher);
    }

    public void Configure(bool value)
    {
        if (enabled == value) return;

        enabled = value;
        coach.Configure(coach.Settings with { Enabled = value });
        Log.Write(value ? "kebiasaan: pengingat aktif" : "kebiasaan: pengingat dimatikan");

        if (value) timer.Start();
        else timer.Stop();
    }

    public void Start()
    {
        if (enabled) timer.Start();
    }

    public void Dispose() => timer.Stop();

    internal void Tick()
    {
        if (!enabled) return;
        if (coach.Tick() is not { } reminder) return;

        Log.Write($"kebiasaan: {reminder.Kind}");
        controller.Refresh(HabitCoach.ActivityFor(reminder, DateTimeOffset.Now, text));
    }
}
