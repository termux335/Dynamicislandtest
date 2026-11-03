using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Status agen AI coding (Tahap 5c): membaca JSON lokal, tidak memanggil
/// jaringan dan tidak menyimpan kunci API. Setuju/tolak hanya menulis satu
/// berkas jawaban di sebelah status.
/// </summary>
public sealed class AgentMonitor : IDisposable
{
    public const string ActivityId = "agent-status";

    private readonly IslandController controller;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;
    private readonly string path;

    private string? last;
    private AgentSnapshot? current;

    public AgentMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null, string? path = null)
    {
        this.controller = controller;
        this.text = text ?? IslandText.Default;
        this.path = path ?? AgentStatusFile.DefaultPath;

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);
    }

    public bool Enabled { get; set; }

    public void Start() => timer.Start();

    public void Respond(bool approved)
    {
        if (current?.Request is not { } request) return;

        if (AgentStatusFile.Respond(path, request.Id, approved))
        {
            Log.Write($"agen: permintaan {request.Id} {(approved ? "disetujui" : "ditolak")}");
        }
        else
        {
            Log.Write("agen: jawaban gagal ditulis");
        }

        current = null;
        last = null;
        controller.Remove(ActivityId);
    }

    public void Dispose()
    {
        timer.Stop();
        controller.Remove(ActivityId);
    }

    private void Poll()
    {
        if (!Enabled)
        {
            if (last is not null)
            {
                last = null;
                current = null;
                controller.Remove(ActivityId);
            }

            return;
        }

        var snapshot = AgentStatusFile.Read(path);
        if (snapshot is null)
        {
            if (last is not null)
            {
                last = null;
                current = null;
                controller.Remove(ActivityId);
            }

            return;
        }

        var key = $"{snapshot.Tool}|{snapshot.State}|{snapshot.Used}|{snapshot.Limit}|{snapshot.Request?.Id}|{snapshot.Request?.Summary}";
        if (key == last) return;

        last = key;
        current = snapshot;
        controller.Refresh(Build(snapshot));
    }

    private Activity Build(AgentSnapshot snapshot)
    {
        var actions = snapshot.Request is null
            ? Array.Empty<ActivityAction>()
            : new[]
            {
                new ActivityAction("agent.approve", text.Get("agent.approve"), "check"),
                new ActivityAction("agent.reject", text.Get("agent.reject"), "close"),
            };

        var state = snapshot.State switch
        {
            "waiting" or "permission" => text.Get("agent.waiting"),
            "working" or "running" => text.Get("agent.working"),
            "error" or "failed" => text.Get("agent.error"),
            _ => text.Get("agent.idle"),
        };

        var subtitle = snapshot.Request?.Summary
            ?? (snapshot.Used is { } used && snapshot.Limit is { } limit
                ? text.Format("agent.usage", used, limit)
                : state);

        return new Activity(
            id: ActivityId,
            kind: ActivityKind.Notification,
            priority: 93,
            title: $"{snapshot.Tool} · {state}",
            subtitle: subtitle,
            actions: actions,
            isLive: true,
            state: ActivityState.Compact);
    }
}
