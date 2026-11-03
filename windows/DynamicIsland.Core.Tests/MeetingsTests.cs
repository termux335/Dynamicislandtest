using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class MeetingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoMicrophoneUserMeansNoActivity()
    {
        Assert.Null(MeetingPlan.ActivityFor(Array.Empty<MicrophoneUser>(), Now, muted: false));
    }

    [Fact]
    public void ActiveMicrophoneShowsTheAppAndElapsedTime()
    {
        var users = new[] { new MicrophoneUser("Teams", Now.AddMinutes(-5)) };

        var activity = MeetingPlan.ActivityFor(users, Now, muted: false)!;

        Assert.Equal(MeetingPlan.ActivityId, activity.Id);
        Assert.Equal(ActivityKind.Call, activity.Kind);
        Assert.Equal("Teams", activity.Title);
        Assert.Equal("Mikrofon aktif · 05:00", activity.Subtitle);
        Assert.True(activity.IsLive);
    }

    [Fact]
    public void MutedMicrophoneSaysSoAndOffersToUnmute()
    {
        var users = new[] { new MicrophoneUser("Teams", Now.AddSeconds(-30)) };

        var activity = MeetingPlan.ActivityFor(users, Now, muted: true)!;

        Assert.Equal("Mikrofon dibisukan · 00:30", activity.Subtitle);
        Assert.Equal("meeting.mic-toggle", activity.Actions[0].Id);
        Assert.Equal("Nyalakan", activity.Actions[0].Label);
        Assert.Equal("unmute", activity.Actions[0].Glyph);
    }

    [Fact]
    public void UnmutedMicrophoneOffersMuteAndBack()
    {
        var users = new[] { new MicrophoneUser("Zoom", Now.AddMinutes(-1)) };

        var activity = MeetingPlan.ActivityFor(users, Now, muted: false)!;

        Assert.Equal(2, activity.Actions.Count);
        Assert.Equal("Bisukan", activity.Actions[0].Label);
        Assert.Equal("mute", activity.Actions[0].Glyph);
        Assert.Equal("meeting.back", activity.Actions[1].Id);
        Assert.Equal("Kembali", activity.Actions[1].Label);
    }

    [Fact]
    public void OldestMicrophoneUserBecomesTheTitle()
    {
        var users = new[]
        {
            new MicrophoneUser("Discord", Now.AddSeconds(-10)),
            new MicrophoneUser("Teams", Now.AddMinutes(-20)),
        };

        Assert.Equal("Teams", MeetingPlan.ActivityFor(users, Now, muted: false)!.Title);
    }

    [Fact]
    public void StartedInTheFutureNeverShowsNegativeTime()
    {
        var users = new[] { new MicrophoneUser("Teams", Now.AddMinutes(2)) };

        Assert.Equal("Mikrofon aktif · 00:00", MeetingPlan.ActivityFor(users, Now, muted: false)!.Subtitle);
    }

    [Fact]
    public void EmptyAppNameFallsBackToTheMeetingLabel()
    {
        var users = new[] { new MicrophoneUser("", Now.AddMinutes(-1)) };

        Assert.Equal("Rapat", MeetingPlan.ActivityFor(users, Now, muted: false)!.Title);
    }

    [Fact]
    public void MeetingTextFollowsLanguageAndHumor()
    {
        var users = new[] { new MicrophoneUser("Teams", Now.AddMinutes(-5)) };
        var kocak = new IslandText(Language.Id, HumorMode.Kocak);

        Assert.Equal("Mic nyala · 05:00. Hati-hati ngomongin bos.", MeetingPlan.ActivityFor(users, Now, muted: false, kocak)!.Subtitle);
    }

    [Fact]
    public void ConsentKeysBecomeShortAppNames()
    {
        Assert.Equal("Teams", MeetingPlan.AppNameFromConsentKey(
            @"C:#Program Files#Microsoft Teams#current#Teams.exe"));
        Assert.Equal("Teams", MeetingPlan.AppNameFromConsentKey("Teams.exe"));
        Assert.Equal("MSTeams_8wekyb3d8bbwe", MeetingPlan.AppNameFromConsentKey("MSTeams_8wekyb3d8bbwe"));
        Assert.Equal(string.Empty, MeetingPlan.AppNameFromConsentKey(""));
    }
}
