using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class HumorBehaviorsTests
{
    // --- EyeGaze ---

    [Fact]
    public void EyeGazeReturnZeroWhenCursorOnCenter()
    {
        var (ox, oy) = EyeGaze.Calculate(100, 100, 100, 100);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void EyeGazeFollowsCursorDirection()
    {
        var (ox, _) = EyeGaze.Calculate(100, 100, 300, 100);
        Assert.True(ox > 0, "pupil harus bergeser ke kanan saat kursor di kanan");
    }

    [Fact]
    public void EyeGazeDoesNotExceedMaxTravel()
    {
        var (ox, oy) = EyeGaze.Calculate(0, 0, 9999, 9999);
        var distance = Math.Sqrt((ox * ox) + (oy * oy));
        Assert.True(distance <= EyeGaze.MaxTravel + 0.01);
    }

    [Fact]
    public void EyeGazeCustomMaxRadius()
    {
        var (ox, oy) = EyeGaze.Calculate(0, 0, 9999, 0, maxRadius: 5.0);
        Assert.True(Math.Abs(ox) <= 5.01);
        Assert.True(Math.Abs(oy) <= 0.01);
    }

    // --- TickleDetector ---

    [Fact]
    public void TickleFiredAfterFiveRapidClicks()
    {
        var detector = new TickleDetector();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++)
            Assert.False(detector.RegisterClick(now.AddMilliseconds(i * 100)));

        Assert.True(detector.RegisterClick(now.AddMilliseconds(400)));
    }

    [Fact]
    public void TickleNotFiredWhenClicksTooSlow()
    {
        var detector = new TickleDetector();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
            Assert.False(detector.RegisterClick(now.AddSeconds(i * 2)));
    }

    [Fact]
    public void TickleResetsAfterFiring()
    {
        var detector = new TickleDetector();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++) detector.RegisterClick(now.AddMilliseconds(i * 50));

        // After firing, buffer is cleared; need 5 more
        Assert.False(detector.RegisterClick(now.AddMilliseconds(300)));
    }

    // --- SulkingDetector ---

    [Fact]
    public void SulkingDetectedAfterTenMinutesIgnored()
    {
        Assert.True(SulkingDetector.IsSulking(
            timerRunning: true,
            timerElapsed: TimeSpan.FromMinutes(12),
            idleDuration: TimeSpan.FromMinutes(11)));
    }

    [Fact]
    public void SulkingNotDetectedWithoutTimer()
    {
        Assert.False(SulkingDetector.IsSulking(
            timerRunning: false,
            timerElapsed: TimeSpan.FromMinutes(12),
            idleDuration: TimeSpan.FromMinutes(11)));
    }

    [Fact]
    public void SulkingNotDetectedWhenRecentInteraction()
    {
        Assert.False(SulkingDetector.IsSulking(
            timerRunning: true,
            timerElapsed: TimeSpan.FromMinutes(12),
            idleDuration: TimeSpan.FromMinutes(2)));
    }

    // --- RepeatTrackDetector ---

    [Fact]
    public void RepeatDetectedOnThirdPlay()
    {
        var detector = new RepeatTrackDetector();

        Assert.False(detector.TrackPlayed("Lagu A", "Artis B"));
        Assert.False(detector.TrackPlayed("Lagu A", "Artis B"));
        Assert.True(detector.TrackPlayed("Lagu A", "Artis B"));
    }

    [Fact]
    public void RepeatResetWhenDifferentTrack()
    {
        var detector = new RepeatTrackDetector();

        detector.TrackPlayed("Lagu A", "Artis B");
        detector.TrackPlayed("Lagu A", "Artis B");
        detector.TrackPlayed("Lagu C", "Artis D");
        Assert.False(detector.TrackPlayed("Lagu A", "Artis B"));
    }

    [Fact]
    public void RepeatIgnoresEmptyTitle()
    {
        var detector = new RepeatTrackDetector();
        Assert.False(detector.TrackPlayed("", null));
        Assert.False(detector.TrackPlayed("  ", null));
    }

    [Fact]
    public void RepeatIsCaseInsensitive()
    {
        var detector = new RepeatTrackDetector();
        Assert.False(detector.TrackPlayed("Song", "Artist"));
        Assert.False(detector.TrackPlayed("SONG", "ARTIST"));
        Assert.True(detector.TrackPlayed("song", "artist"));
    }

    // --- DramaTimer ---

    [Theory]
    [InlineData(10, true)]
    [InlineData(5, true)]
    [InlineData(1, true)]
    [InlineData(11, false)]
    [InlineData(0, false)]
    public void DramaticThreshold(int seconds, bool expected)
    {
        Assert.Equal(expected, DramaTimer.IsDramatic(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void PulseFrequencyIncreasesNearZero()
    {
        var freqAt10 = DramaTimer.PulseFrequency(TimeSpan.FromSeconds(10));
        var freqAt1 = DramaTimer.PulseFrequency(TimeSpan.FromSeconds(1));

        Assert.True(freqAt1 > freqAt10, "pulse harus lebih cepat saat mendekati 0");
        Assert.True(freqAt10 >= 1.0);
        Assert.True(freqAt1 <= 4.1);
    }

    [Fact]
    public void PulseFrequencyZeroOutsideThreshold()
    {
        Assert.Equal(0, DramaTimer.PulseFrequency(TimeSpan.FromSeconds(15)));
        Assert.Equal(0, DramaTimer.PulseFrequency(TimeSpan.Zero));
    }

    // --- EasterEggSequence ---

    [Fact]
    public void EasterEggTriggeredByCorrectSequence()
    {
        var egg = new EasterEggSequence("island");
        var now = DateTimeOffset.UtcNow;

        foreach (var c in "islan")
            Assert.False(egg.RegisterKey(c, now));

        Assert.True(egg.RegisterKey('d', now));
    }

    [Fact]
    public void EasterEggNotTriggeredByPartialSequence()
    {
        var egg = new EasterEggSequence("island");
        var now = DateTimeOffset.UtcNow;

        foreach (var c in "isla")
            egg.RegisterKey(c, now);

        // Wrong key breaks it
        Assert.False(egg.RegisterKey('x', now));
    }

    [Fact]
    public void EasterEggTimesOutAfterDelay()
    {
        var egg = new EasterEggSequence("ab");
        var now = DateTimeOffset.UtcNow;

        egg.RegisterKey('a', now);
        Assert.False(egg.RegisterKey('b', now.AddSeconds(5)));
    }

    [Fact]
    public void EasterEggResetsAfterTrigger()
    {
        var egg = new EasterEggSequence("ab");
        var now = DateTimeOffset.UtcNow;

        egg.RegisterKey('a', now);
        Assert.True(egg.RegisterKey('b', now));

        // Buffer cleared; need full sequence again
        Assert.False(egg.RegisterKey('b', now));
    }
}

public class QuickActionCatalogTests
{
    [Fact]
    public void ParseReturnsDefaultsForNull()
    {
        var result = QuickActionCatalog.Parse(null);
        Assert.Equal(QuickActionCatalog.DefaultActionIds, result);
    }

    [Fact]
    public void ParseReturnsDefaultsForEmptyArray()
    {
        var result = QuickActionCatalog.Parse("[]");
        Assert.Equal(QuickActionCatalog.DefaultActionIds, result);
    }

    [Fact]
    public void ParseFiltersUnknownIds()
    {
        var result = QuickActionCatalog.Parse("""["media-toggle", "unknown-thing", "screenshot"]""");
        Assert.Equal(2, result.Count);
        Assert.Contains("media-toggle", result);
        Assert.Contains("screenshot", result);
        Assert.DoesNotContain("unknown-thing", result);
    }

    [Fact]
    public void ParseDeduplicates()
    {
        var result = QuickActionCatalog.Parse("""["media-toggle", "media-toggle"]""");
        Assert.Single(result);
    }

    [Fact]
    public void SerializeRoundTrips()
    {
        var ids = new[] { "media-toggle", "screenshot" };
        var json = QuickActionCatalog.Serialize(ids);
        var parsed = QuickActionCatalog.Parse(json);
        Assert.Equal(ids, parsed);
    }
}
