namespace DynamicIsland.Core;

public enum StateTransition
{
    Open,
    Close,
    AutoCollapse,
}

/// <summary>
/// Transisi state minimal &lt;-&gt; compact &lt;-&gt; expanded (lihat <c>shared/SPEC.md</c> bagian 4).
///
/// Aturan:
/// - state awal saat aktivitas masuk: compact;
/// - membuka selalu satu langkah (minimal -&gt; compact -&gt; expanded);
/// - menutup selalu satu langkah (expanded -&gt; compact -&gt; minimal);
/// - ciut otomatis hanya menurunkan expanded ke compact, tidak pernah ke minimal.
/// </summary>
public static class StateMachine
{
    /// <summary>State awal untuk aktivitas baru.</summary>
    public const ActivityState InitialState = ActivityState.Compact;

    /// <summary>Lama tanpa interaksi sebelum expanded ciut sendiri.</summary>
    public static TimeSpan AutoCollapseDelay { get; } = TimeSpan.FromSeconds(4);

    /// <summary>State berikutnya, atau null bila tidak ada perubahan yang sah.</summary>
    public static ActivityState? Next(ActivityState current, StateTransition transition) => transition switch
    {
        StateTransition.Open => current.Opened(),
        StateTransition.Close => current.Closed(),
        StateTransition.AutoCollapse => current == ActivityState.Expanded ? ActivityState.Compact : null,
        _ => null,
    };

    /// <summary>
    /// Langkah-langkah menuju state tujuan. Lompatan dipecah per langkah,
    /// misalnya minimal -&gt; expanded menjadi [compact, expanded].
    /// </summary>
    public static IReadOnlyList<ActivityState> Steps(ActivityState from, ActivityState to)
    {
        if (from == to) return Array.Empty<ActivityState>();

        var steps = new List<ActivityState>();
        var cursor = from;
        var ascending = to.Depth() > from.Depth();

        while (cursor != to)
        {
            var step = ascending ? cursor.Opened() : cursor.Closed();
            if (step is null) break;
            steps.Add(step.Value);
            cursor = step.Value;
        }

        return steps;
    }
}
