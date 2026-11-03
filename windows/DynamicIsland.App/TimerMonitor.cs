using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Menjalankan <see cref="TimerEngine"/> dan menjaga satu aktivitas timer di
/// antrean. Selesai hitung mundur → timer dicabut dan muncul alert 4 detik.
/// </summary>
public sealed class TimerMonitor : IDisposable
{
    private readonly IslandController controller;
    private readonly TimerEngine engine;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;

    private string? lastTitle;
    private string? lastSubtitle;
    private bool shown;

    public TimerMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        this.text = text ?? IslandText.Default;
        engine = new TimerEngine();
        timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => Tick(),
            dispatcher);
    }

    public bool IsActive => engine.IsActive;

    public bool IsRunning => engine.IsRunning;

    public void Start() => timer.Start();

    public void StartFocus()
    {
        engine.StartCountdown(TimerEngine.PomodoroLength);
        Log.Write("timer: fokus 25 menit dimulai");
        Sync(force: true);
    }

    public void StartStopwatch()
    {
        engine.StartStopwatch();
        Log.Write("timer: stopwatch dimulai");
        Sync(force: true);
    }

    public void ToggleTimer()
    {
        engine.Toggle();
        Sync(force: true);
    }

    public void ResetTimer()
    {
        engine.Reset();
        Sync(force: true);
    }

    public void StopTimer()
    {
        engine.Stop();
        Remove();
        Log.Write("timer: dihentikan");
    }

    public void Dispose() => timer.Stop();

    private void Tick()
    {
        if (!engine.IsActive)
        {
            if (shown) Remove();
            return;
        }

        if (engine.Tick())
        {
            engine.Stop();
            Remove();
            controller.Refresh(TimerAlert.Done(DateTimeOffset.Now, text));
            Log.Write("timer: selesai");
            return;
        }

        Sync(force: false);
    }

    /// <summary>Memperbarui aktivitas hanya saat teks berubah (sekali per detik).</summary>
    private void Sync(bool force)
    {
        var activity = engine.Snapshot(text);
        if (activity is null)
        {
            Remove();
            return;
        }

        if (!force && activity.Title == lastTitle && activity.Subtitle == lastSubtitle) return;

        lastTitle = activity.Title;
        lastSubtitle = activity.Subtitle;
        shown = true;
        controller.Refresh(activity);
    }

    private void Remove()
    {
        lastTitle = null;
        lastSubtitle = null;
        shown = false;
        controller.Remove(TimerEngine.ActivityId);
    }
}
