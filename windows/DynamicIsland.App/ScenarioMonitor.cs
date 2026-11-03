using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Skenario otomatis (Tahap 4): rapat, belajar, dan charging malam.
///
/// Rapat mengikuti deteksi mikrofon, belajar mengikuti saklar pengguna, dan
/// charging malam hanya saat mengisi daya pada jam malam.
/// </summary>
internal sealed class ScenarioMonitor : IDisposable
{
    private readonly IslandController controller;
    private readonly IslandText text;
    private readonly DispatcherTimer timer;
    private readonly Func<IslandContext> context;
    private bool shownNight;

    public ScenarioMonitor(
        IslandController controller,
        Dispatcher dispatcher,
        IslandText text,
        Func<IslandContext> context)
    {
        this.controller = controller;
        this.text = text;
        this.context = context;

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(20),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    public Func<bool> StudyMode { get; set; } = () => false;

    public ScenarioPlan Plan { get; private set; } = ScenarioPlan.None;

    public event Action<ScenarioPlan>? PlanChanged;

    public void Start()
    {
        Poll();
        timer.Start();
    }

    public void Dispose() => timer.Stop();

    internal void Poll()
    {
        var charging = BatteryMonitor.TryRead(out var state) && state.Charging;

        var plan = ScenarioRules.Evaluate(new ScenarioInput(
            context(),
            charging,
            StudyMode(),
            DateTimeOffset.Now));

        if (plan != Plan)
        {
            Plan = plan;
            Log.Write($"skenario: {plan.Kind} (notifikasi ditahan: {plan.SuppressNotifications})");
            PlanChanged?.Invoke(plan);
        }

        if (!plan.ShowsClock)
        {
            if (!shownNight) return;

            shownNight = false;
            controller.Remove(ScenarioRules.NightActivityId);
            return;
        }

        shownNight = true;
        controller.Refresh(ScenarioRules.NightClock(DateTimeOffset.Now, text));
    }
}
