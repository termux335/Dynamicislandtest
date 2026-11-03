using System.IO;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Status agen AI coding (seperti Coucou): membaca JSON lokal, bereaksi instan
/// lewat FileSystemWatcher, dan menampilkan kartu persetujuan interaktif (1-click approve/deny).
/// Tanpa kunci API dan tanpa panggilan internet eksternal.
/// </summary>
public sealed class AgentMonitor : IDisposable
{
    public const string ActivityId = "agent-status";

    private readonly IslandController controller;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;
    private readonly string path;
    private FileSystemWatcher? watcher;

    private string? last;
    private AgentSnapshot? current;

    public AgentMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null, string? path = null)
    {
        this.controller = controller;
        this.dispatcher = dispatcher;
        this.text = text ?? IslandText.Default;
        this.path = path ?? AgentStatusFile.DefaultPath;

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Poll(),
            dispatcher);

        InitWatcher();
    }

    public bool Enabled { get; set; }

    public void Start()
    {
        timer.Start();
        if (watcher is not null) watcher.EnableRaisingEvents = true;
        Poll();
    }

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
        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            watcher = null;
        }
        controller.Remove(ActivityId);
    }

    private void InitWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };

                watcher.Changed += (_, _) => _ = dispatcher.BeginInvoke(Poll);
                watcher.Created += (_, _) => _ = dispatcher.BeginInvoke(Poll);
                watcher.Deleted += (_, _) => _ = dispatcher.BeginInvoke(Poll);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"agen: watcher gagal dimulai ({ex.Message})");
        }
    }

    public void Poll()
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

        if (snapshot.State is "idle" && snapshot.Request is null)
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
        var hasRequest = snapshot.Request is not null;
        var actions = !hasRequest
            ? Array.Empty<ActivityAction>()
            : new[]
            {
                new ActivityAction("agent.approve", "✓ " + text.Get("agent.approve"), "check"),
                new ActivityAction("agent.reject", "✕ " + text.Get("agent.reject"), "close"),
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
            priority: hasRequest ? 98 : 93,
            title: $"{snapshot.Tool} · {state}",
            subtitle: subtitle,
            actions: actions,
            caption: snapshot.Request?.Detail ?? snapshot.Request?.Summary,
            isLive: true,
            state: hasRequest ? ActivityState.Expanded : ActivityState.Compact);
    }
}
