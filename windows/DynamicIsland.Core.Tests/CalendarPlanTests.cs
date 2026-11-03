using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

/// <summary>Tes acara kalender berikutnya dan pembaca <c>.ics</c> lokal (Tahap 2).</summary>
public class CalendarPlanTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static CalendarEvent Event(string title, TimeSpan inMinutes, TimeSpan lasts, string? url = null) =>
        new(title, title, Start + inMinutes, Start + inMinutes + lasts, url);

    [Fact]
    public void EarliestUnfinishedEventWins()
    {
        var events = new[]
        {
            Event("rapat B", TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(30)),
            Event("rapat A", TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)),
            Event("sudah lewat", TimeSpan.FromMinutes(-120), TimeSpan.FromMinutes(10)),
        };

        var next = CalendarPlan.Next(events, Start);

        Assert.Equal("rapat A", next?.Title);
    }

    [Fact]
    public void EventInProgressIsStillNextUntilItEnds()
    {
        var running = Event("rapat A", TimeSpan.FromMinutes(-10), TimeSpan.FromMinutes(60));

        Assert.Equal("rapat A", CalendarPlan.Next(new[] { running }, Start)?.Title);
        Assert.Null(CalendarPlan.Next(new[] { running }, Start + TimeSpan.FromMinutes(50)));
    }

    [Fact]
    public void CountdownSwitchesToEndTimeWhenTheEventStarted()
    {
        var upcoming = Event("rapat A", TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60));
        var running = Event("rapat A", TimeSpan.FromMinutes(-5), TimeSpan.FromMinutes(60));

        Assert.Equal("mulai dalam 15 menit", CalendarPlan.CountdownLabel(upcoming, Start));
        Assert.Equal("berakhir dalam 55 menit", CalendarPlan.CountdownLabel(running, Start));
    }

    [Fact]
    public void DurationsUseIndonesianWording()
    {
        Assert.Equal("2 jam", CalendarPlan.Duration(TimeSpan.FromHours(2)));
        Assert.Equal("1 jam 30 menit", CalendarPlan.Duration(TimeSpan.FromMinutes(90)));
        Assert.Equal("15 menit", CalendarPlan.Duration(TimeSpan.FromSeconds(900)));
        Assert.Equal("45 detik", CalendarPlan.Duration(TimeSpan.FromSeconds(45)));
        Assert.Equal("1 detik", CalendarPlan.Duration(TimeSpan.Zero));
    }

    [Fact]
    public void ActivityOffersJoinOnlyWhenThereIsALink()
    {
        var withLink = CalendarPlan.ActivityFor(Event("rapat A", TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), "https://meet.example/abc"), Start);
        var withoutLink = CalendarPlan.ActivityFor(Event("rapat A", TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)), Start);

        Assert.Equal(CalendarPlan.Id, withLink.Id);
        Assert.Equal(ActivityKind.Calendar, withLink.Kind);
        Assert.True(withLink.IsLive);
        Assert.Equal("mulai dalam 15 menit", withLink.Subtitle);
        Assert.Equal("join", Assert.Single(withLink.Actions).Id);
        Assert.Empty(withoutLink.Actions);
    }

    [Fact]
    public void MeetingLinkComesFromDescriptionThenLocation()
    {
        Assert.Equal(
            "https://zoom.example/j/123",
            CalendarPlan.MeetingUrl("Rapat: https://zoom.example/j/123.", null));
        Assert.Equal(
            "https://teams.example/abc",
            CalendarPlan.MeetingUrl(null, "Ruang 4 https://teams.example/abc"));
        Assert.Null(CalendarPlan.MeetingUrl("tanpa tautan", "Ruang 4"));
    }

    [Fact]
    public void IcsParserReadsEventsAndIgnoresBrokenOnes()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:abc-1
            SUMMARY:Rapat Produk
            DTSTART:20260101T093000Z
            DTEND:20260101T101500Z
            DESCRIPTION:Gabung di https://meet.example/abc
            END:VEVENT
            BEGIN:VEVENT
            SUMMARY:Sudah lewat
            DTSTART:20250101T090000Z
            DTEND:20250101T100000Z
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendar.Parse(ics);
        var next = CalendarPlan.Next(events, Start);

        Assert.Equal(2, events.Count);
        Assert.NotNull(next);
        Assert.Equal("abc-1", next!.Id);
        Assert.Equal("Rapat Produk", next.Title);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero), next.StartAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 15, 0, TimeSpan.Zero), next.EndAt);
        Assert.Equal("https://meet.example/abc", next.MeetingUrl);
    }

    [Fact]
    public void IcsParserHandlesFoldedLinesAndEscapes()
    {
        const string ics = "BEGIN:VEVENT\r\nSUMMARY:Rapat panjang yang\r\n  dilipat\r\nDTSTART;VALUE=DATE:20260102\r\nEND:VEVENT\r\n";

        var item = Assert.Single(IcsCalendar.Parse(ics));

        Assert.Equal("Rapat panjang yang dilipat", item.Title);
        // VALUE=DATE berarti sepanjang hari menurut zona waktu lokal.
        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0), item.StartAt.DateTime);
        // Tanpa DTEND: durasi bawaan 30 menit.
        Assert.Equal(item.StartAt.AddMinutes(30), item.EndAt);
    }

    [Fact]
    public void IcsParserSkipsEventsWithoutAStart()
    {
        const string ics = """
            BEGIN:VEVENT
            SUMMARY:Tanpa waktu
            END:VEVENT
            """;

        Assert.Empty(IcsCalendar.Parse(ics));
        Assert.Empty(IcsCalendar.Parse(null));
    }
}
