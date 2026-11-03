namespace DynamicIsland.Core;

/// <summary>Jenis timer yang sedang berjalan.</summary>
public enum TimerMode
{
    /// <summary>Hitung mundur (pomodoro).</summary>
    Countdown,

    /// <summary>Stopwatch naik terus.</summary>
    Stopwatch,
}

/// <summary>
/// Timer asli untuk kind <see cref="ActivityKind.Timer"/>: hitung mundur dan
/// stopwatch, dengan jeda, lanjut, dan ulang. Waktu masuk lewat parameter
/// <c>now</c>, jadi tes tidak butuh timer sistem.
/// </summary>
public sealed class TimerEngine
{
    /// <summary>Id aktivitas timer di antrean.</summary>
    public const string ActivityId = "timer";

    /// <summary>Panjang sesi fokus bawaan (pomodoro).</summary>
    public static readonly TimeSpan PomodoroLength = TimeSpan.FromMinutes(25);

    private readonly Func<DateTimeOffset> now;

    private TimeSpan duration;
    private TimeSpan accumulated;
    private DateTimeOffset? runningSince;
    private bool completed;

    public TimerEngine(Func<DateTimeOffset>? now = null)
    {
        this.now = now ?? (() => DateTimeOffset.Now);
    }

    public TimerMode? Mode { get; private set; }

    public bool IsRunning => runningSince is not null;

    public bool IsActive => Mode is not null;

    /// <summary>Panjang hitung mundur. Nol untuk stopwatch.</summary>
    public TimeSpan Duration => duration;

    /// <summary>Waktu berjalan (stopwatch) atau terpakai (hitung mundur), termasuk jeda.</summary>
    public TimeSpan Elapsed => accumulated + (runningSince is { } since ? now() - since : TimeSpan.Zero);

    /// <summary>Sisa waktu hitung mundur; nol untuk stopwatch.</summary>
    public TimeSpan Remaining
    {
        get
        {
            if (Mode != TimerMode.Countdown) return TimeSpan.Zero;

            var left = duration - Elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>Mulai hitung mundur baru. Durasi nol atau negatif diabaikan.</summary>
    public void StartCountdown(TimeSpan length)
    {
        if (length <= TimeSpan.Zero) return;

        Mode = TimerMode.Countdown;
        duration = length;
        accumulated = TimeSpan.Zero;
        runningSince = now();
        completed = false;
    }

    public void StartStopwatch()
    {
        Mode = TimerMode.Stopwatch;
        duration = TimeSpan.Zero;
        accumulated = TimeSpan.Zero;
        runningSince = now();
        completed = false;
    }

    /// <summary>Jeda bila sedang jalan, lanjut bila sedang berhenti.</summary>
    public void Toggle()
    {
        if (Mode is null) return;

        if (runningSince is { } since)
        {
            accumulated += now() - since;
            runningSince = null;
        }
        else
        {
            runningSince = now();
        }
    }

    /// <summary>Kembalikan timer ke nol tanpa mengubah jenisnya.</summary>
    public void Reset()
    {
        accumulated = TimeSpan.Zero;
        runningSince = Mode is null ? null : now();
        completed = false;
    }

    /// <summary>Matikan timer dan kosongkan state.</summary>
    public void Stop()
    {
        Mode = null;
        duration = TimeSpan.Zero;
        accumulated = TimeSpan.Zero;
        runningSince = null;
        completed = false;
    }

    /// <summary>True sekali saat hitung mundur mencapai nol.</summary>
    public bool Tick()
    {
        if (Mode != TimerMode.Countdown || completed) return false;
        if (Remaining > TimeSpan.Zero) return false;

        completed = true;
        accumulated = duration;
        runningSince = null;
        return true;
    }

    /// <summary>Aktivitas untuk antrean, atau null bila timer tidak aktif.</summary>
    public Activity? Snapshot(IslandText? text = null)
    {
        if (Mode is not { } mode) return null;

        var strings = text ?? IslandText.Default;
        var countdown = mode == TimerMode.Countdown;
        var shown = countdown ? Remaining : Elapsed;

        var progress = countdown && duration > TimeSpan.Zero
            ? Math.Clamp((duration - shown) / duration, 0, 1)
            : (double?)null;

        return new Activity(
            id: ActivityId,
            kind: ActivityKind.Timer,
            title: strings.Get(countdown ? "timer.title.focus" : "timer.title.stopwatch"),
            subtitle: Format(shown),
            progress: progress,
            actions: new[]
            {
                new ActivityAction(
                    "timer-toggle",
                    strings.Get(IsRunning ? "timer.action.pause" : "timer.action.resume"),
                    IsRunning ? "pause" : "play"),
                new ActivityAction("timer-reset", strings.Get("timer.action.reset"), "reset"),
            });
    }

    /// <summary>Format mm:ss, atau j:mm:ss bila satu jam ke atas.</summary>
    public static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }
}

/// <summary>Alert singkat saat hitung mundur selesai.</summary>
public static class TimerAlert
{
    public const string Id = "timer-done";

    public static Activity Done(DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: Id,
            kind: ActivityKind.Notification,
            title: strings.Get("alert.timerDone.title"),
            subtitle: strings.Get("alert.timerDone.subtitle"),
            isLive: false,
            expiresAt: now.AddMilliseconds(IslandEvent.DefaultTtlMs));
    }
}
