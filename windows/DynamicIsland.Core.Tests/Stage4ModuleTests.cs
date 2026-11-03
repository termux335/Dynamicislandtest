using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class Stage4ModuleTests
{
    // --- Skenario otomatis ---

    [Fact]
    public void MeetingScenarioWinsOverStudyMode()
    {
        var plan = ScenarioRules.Evaluate(new ScenarioInput(
            Context: IslandContext.Meeting,
            StudyMode: true,
            Now: new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.FromHours(7))));

        Assert.Equal(ScenarioKind.Meeting, plan.Kind);
        Assert.False(plan.SuppressNotifications);
        Assert.Equal(ActivityKind.Call, plan.FocusKind);
    }

    [Fact]
    public void StudyModeBlocksNonCriticalNotifications()
    {
        var plan = ScenarioRules.Evaluate(new ScenarioInput(
            StudyMode: true,
            Now: new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.FromHours(7))));

        Assert.Equal(ScenarioKind.Study, plan.Kind);
        Assert.True(plan.SuppressNotifications);
    }

    [Fact]
    public void NightChargingOnlyHappensAtNight()
    {
        var night = ScenarioRules.Evaluate(new ScenarioInput(
            Charging: true,
            Now: new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.FromHours(7))));

        var day = ScenarioRules.Evaluate(new ScenarioInput(
            Charging: true,
            Now: new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.FromHours(7))));

        Assert.Equal(ScenarioKind.NightCharging, night.Kind);
        Assert.True(night.ShowsClock);
        Assert.Equal(ScenarioKind.None, day.Kind);
    }

    [Fact]
    public void NightClockShowsLocalTime()
    {
        var now = new DateTimeOffset(2026, 11, 3, 23, 5, 0, TimeSpan.FromHours(7));

        var clock = ScenarioRules.NightClock(now);

        Assert.Equal("23:05", clock.Title);
        Assert.Equal(ScenarioRules.NightActivityId, clock.Id);
        Assert.Equal(5, clock.Priority);
    }

    // --- Pengingat kebiasaan ---

    [Fact]
    public void DisabledHabitCoachNeverReminds()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var coach = new HabitCoach(new HabitSettings(Enabled: false), () => now);

        now = now.AddHours(3);

        Assert.Null(coach.Tick());
        Assert.Null(coach.NextIn());
    }

    [Fact]
    public void EyeRestFiresAfterTwentyMinutes()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var coach = new HabitCoach(new HabitSettings(Enabled: true), () => now);

        Assert.Null(coach.Tick());

        now = now.AddMinutes(19);
        Assert.Null(coach.Tick());

        now = now.AddMinutes(1);
        var reminder = coach.Tick();

        Assert.NotNull(reminder);
        Assert.Equal(HabitKind.EyeRest, reminder!.Kind);
        Assert.Equal("Istirahatkan mata", reminder.Title);
    }

    [Fact]
    public void DrinkReminderFiresAfterFortyFiveMinutes()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var coach = new HabitCoach(new HabitSettings(Enabled: true), () => now);

        coach.Tick();

        now = now.AddMinutes(20);
        Assert.Equal(HabitKind.EyeRest, coach.Tick()?.Kind);

        now = now.AddMinutes(25);
        Assert.Equal(HabitKind.Drink, coach.Tick()?.Kind);
    }

    [Fact]
    public void ReminderBecomesATransientCapsuleAlert()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var reminder = new HabitReminder(HabitKind.Drink, "Waktunya minum", "Segelas air", now);

        var activity = HabitCoach.ActivityFor(reminder, now);

        Assert.Equal(ActivityKind.Notification, activity.Kind);
        Assert.False(activity.IsLive);
        Assert.Equal(now.AddSeconds(8), activity.ExpiresAt);
    }

    // --- Jadwal salat ---

    [Fact]
    public void JakartaPrayerTimesAreInOrderAndPlausible()
    {
        var times = PrayerCalculator.For(new DateOnly(2026, 11, 3), PrayerLocation.Jakarta);

        Assert.Equal(6, times.Count);
        Assert.Equal(times.OrderBy(time => time.At).Select(time => time.Prayer), times.Select(time => time.Prayer));

        var fajr = times.Single(time => time.Prayer == Prayer.Fajr).At;
        var dhuhr = times.Single(time => time.Prayer == Prayer.Dhuhr).At;
        var maghrib = times.Single(time => time.Prayer == Prayer.Maghrib).At;

        Assert.InRange(fajr.Hour, 3, 5);
        Assert.InRange(dhuhr.Hour, 11, 12);
        Assert.InRange(maghrib.Hour, 17, 18);
    }

    [Fact]
    public void AsrFallsBetweenDhuhrAndMaghrib()
    {
        var times = PrayerCalculator.For(new DateOnly(2026, 11, 3), PrayerLocation.Jakarta);

        var dhuhr = times.Single(time => time.Prayer == Prayer.Dhuhr).At;
        var asr = times.Single(time => time.Prayer == Prayer.Asr).At;
        var maghrib = times.Single(time => time.Prayer == Prayer.Maghrib).At;

        Assert.True(dhuhr < asr && asr < maghrib);
    }

    [Fact]
    public void NextPrayerIsTheFirstOneAhead()
    {
        var times = PrayerCalculator.For(new DateOnly(2026, 11, 3), PrayerLocation.Jakarta);
        var now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(7));

        var next = PrayerPlan.Next(times, now);

        Assert.NotNull(next);
        Assert.Equal(Prayer.Dhuhr, next!.Value.Prayer);
    }

    [Fact]
    public void PrayerActivityShowsTheCountdown()
    {
        var now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(7));
        var next = new PrayerTime(Prayer.Dhuhr, now.AddMinutes(95));

        var activity = PrayerPlan.ActivityFor(next, now);

        Assert.NotNull(activity);
        Assert.Equal("Zuhur", activity!.Title);
        Assert.NotNull(activity.Subtitle);
        Assert.Contains("1 jam 35", activity.Subtitle!);
    }

    // --- Cuaca BMKG ---

    private const string BmkgSample = """
        {
          "data": [
            { "cuaca": [ [
              { "local_datetime": "2026-11-03T12:00:00+07:00", "weather_desc": "Berawan", "t": 31 },
              { "local_datetime": "2026-11-03T14:00:00+07:00", "weather_desc": "Hujan Petir", "t": 28 }
            ] ] }
          ]
        }
        """;

    [Fact]
    public void NestedBmkgForecastIsParsed()
    {
        var points = BmkgWeather.Parse(BmkgSample);

        Assert.Equal(2, points.Count);
        Assert.Equal("Berawan", points[0].Description);
        Assert.Equal(31, points[0].TemperatureC);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.FromHours(7)), points[1].At);
    }

    [Fact]
    public void SevereWeatherWithinTheHorizonIsWarned()
    {
        var points = BmkgWeather.Parse(BmkgSample);
        var now = new DateTimeOffset(2026, 11, 3, 12, 30, 0, TimeSpan.FromHours(7));

        var warning = BmkgWeather.Warning(points, now);

        Assert.NotNull(warning);
        Assert.Equal("Hujan Petir", warning!.Description);
    }

    [Fact]
    public void LightWeatherIsNotAWarning()
    {
        var points = BmkgWeather.Parse("""
            [ { "local_datetime": "2026-11-03T13:00:00+07:00", "weather_desc": "Berawan" } ]
            """);

        Assert.Null(BmkgWeather.Warning(
            points,
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(7))));
    }

    [Fact]
    public void WarningOutsideTheHorizonIsIgnored()
    {
        var points = BmkgWeather.Parse("""
            [ { "local_datetime": "2026-11-04T02:00:00+07:00", "weather_desc": "Badai" } ]
            """);

        Assert.Null(BmkgWeather.Warning(
            points,
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(7))));
    }

    // --- Pelindung OLED ---

    [Fact]
    public void MicroShiftChangesEveryFourMinutes()
    {
        Assert.Equal((0, 0), OledGuard.Shift(TimeSpan.Zero));
        Assert.Equal((2, 0), OledGuard.Shift(TimeSpan.FromMinutes(4)));
        Assert.Equal((2, 2), OledGuard.Shift(TimeSpan.FromMinutes(8)));
        Assert.Equal((0, 2), OledGuard.Shift(TimeSpan.FromMinutes(12)));
        Assert.Equal((0, 0), OledGuard.Shift(TimeSpan.FromMinutes(32)));
    }

    [Fact]
    public void IdleDimsThenHides()
    {
        Assert.Equal(OledPhase.Active, OledGuard.StateFor(TimeSpan.FromSeconds(10), TimeSpan.Zero).Phase);

        var dimmed = OledGuard.StateFor(TimeSpan.FromSeconds(90), TimeSpan.Zero);
        Assert.Equal(OledPhase.Dimmed, dimmed.Phase);
        Assert.Equal(0.65, dimmed.Opacity);
        Assert.True(dimmed.Visible);

        var hidden = OledGuard.StateFor(TimeSpan.FromMinutes(11), TimeSpan.Zero);
        Assert.Equal(OledPhase.Hidden, hidden.Phase);
        Assert.False(hidden.Visible);
    }

    [Fact]
    public void DisabledOledGuardLeavesEverythingAlone()
    {
        var state = OledGuard.StateFor(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), enabled: false);

        Assert.Equal(OledPhase.Active, state.Phase);
        Assert.Equal(1, state.Opacity);
        Assert.True(state.Visible);
        Assert.Equal((0, 0), (state.OffsetX, state.OffsetY));
    }
}
