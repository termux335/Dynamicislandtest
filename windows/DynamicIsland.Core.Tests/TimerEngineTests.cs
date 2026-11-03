using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class TimerEngineTests
{
    private sealed class TestClock
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Read() => Now;

        public void Advance(TimeSpan delta) => Now += delta;
    }

    private static (TimerEngine Engine, TestClock Clock) Create()
    {
        var clock = new TestClock();
        return (new TimerEngine(clock.Read), clock);
    }

    [Fact]
    public void CountdownCountsDownAndReportsProgress()
    {
        var (engine, clock) = Create();
        engine.StartCountdown(TimeSpan.FromMinutes(25));

        clock.Advance(TimeSpan.FromSeconds(90));

        var activity = engine.Snapshot();
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Timer, activity!.Kind);
        Assert.Equal("Fokus", activity.Title);
        Assert.Equal("23:30", activity.Subtitle);
        Assert.Equal(TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(90), engine.Remaining);
        Assert.NotNull(activity.Progress);
        Assert.Equal(90.0 / 1500.0, activity.Progress!.Value, 3);
    }

    [Fact]
    public void PauseFreezesTheClockAndResumeContinues()
    {
        var (engine, clock) = Create();
        engine.StartCountdown(TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromSeconds(30));
        engine.Toggle();
        Assert.False(engine.IsRunning);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(30), engine.Remaining);

        engine.Toggle();
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromMinutes(10) - TimeSpan.FromMinutes(1), engine.Remaining);
    }

    [Fact]
    public void CompletionIsReportedOnceAtZero()
    {
        var (engine, clock) = Create();
        engine.StartCountdown(TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(engine.Tick());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(engine.Tick());
        Assert.False(engine.Tick());
        Assert.False(engine.IsRunning);
        Assert.Equal(TimeSpan.Zero, engine.Remaining);
        Assert.Equal("00:00", engine.Snapshot()?.Subtitle);
    }

    [Fact]
    public void StopwatchCountsUpWithoutProgress()
    {
        var (engine, clock) = Create();
        engine.StartStopwatch();

        clock.Advance(TimeSpan.FromSeconds(75));

        var activity = engine.Snapshot();
        Assert.Equal("Stopwatch", activity?.Title);
        Assert.Equal("01:15", activity?.Subtitle);
        Assert.Null(activity?.Progress);
    }

    [Fact]
    public void ResetKeepsTheModeButReturnsToZero()
    {
        var (engine, clock) = Create();
        engine.StartCountdown(TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(2));

        engine.Reset();

        Assert.Equal(TimerMode.Countdown, engine.Mode);
        Assert.True(engine.IsRunning);
        Assert.Equal("05:00", engine.Snapshot()?.Subtitle);
    }

    [Fact]
    public void StopClearsTheTimer()
    {
        var (engine, _) = Create();
        engine.StartCountdown(TimeSpan.FromMinutes(5));

        engine.Stop();

        Assert.False(engine.IsActive);
        Assert.Null(engine.Snapshot());
    }

    [Fact]
    public void ToggleAndStopWithoutATimerAreHarmless()
    {
        var (engine, _) = Create();

        engine.Toggle();
        engine.Reset();
        engine.Stop();

        Assert.False(engine.IsActive);
        Assert.False(engine.Tick());
    }

    [Fact]
    public void ActionsToggleTheirLabelWithTheRunningState()
    {
        var (engine, _) = Create();
        engine.StartCountdown(TimeSpan.FromMinutes(25));
        Assert.Equal("Jeda", engine.Snapshot()?.Actions[0].Label);

        engine.Toggle();
        Assert.Equal("Lanjut", engine.Snapshot()?.Actions[0].Label);
    }

    [Theory]
    [InlineData(0, 0, "00:00")]
    [InlineData(0, 59, "00:59")]
    [InlineData(24, 38, "24:38")]
    [InlineData(59, 59, "59:59")]
    public void FormatUsesMinutesAndSeconds(int minutes, int seconds, string expected)
    {
        Assert.Equal(expected, TimerEngine.Format(new TimeSpan(0, minutes, seconds)));
    }

    [Fact]
    public void FormatAddsHoursOnlyWhenNeeded()
    {
        Assert.Equal("1:05:09", TimerEngine.Format(new TimeSpan(1, 5, 9)));
        Assert.Equal("00:00", TimerEngine.Format(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void CompletionAlertIsATransientNotification()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var alert = TimerAlert.Done(now);

        Assert.Equal(TimerAlert.Id, alert.Id);
        Assert.False(alert.IsLive);
        Assert.Equal(now.AddMilliseconds(IslandEvent.DefaultTtlMs), alert.ExpiresAt);
        Assert.Equal("Timer selesai", alert.Title);
    }
}
