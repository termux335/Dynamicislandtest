using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class LyricsTests
{
    [Fact]
    public void ParsesTimestampsAndText()
    {
        var lines = Lyrics.Parse("""
            [00:12.50]Baris pertama
            [00:15.00]Baris kedua
            """);

        Assert.Equal(2, lines.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(12500), lines[0].At);
        Assert.Equal("Baris pertama", lines[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(15), lines[1].At);
    }

    [Fact]
    public void HandlesMinutesPastNineAndHours()
    {
        var lines = Lyrics.Parse("[12:05]Menit dua belas\n[75:00]Lewat sejam");

        Assert.Equal(TimeSpan.FromSeconds(725), lines[0].At);
        Assert.Equal(TimeSpan.FromMinutes(75), lines[1].At);
    }

    [Fact]
    public void OneLineWithSeveralStampsBecomesSeveralLines()
    {
        var lines = Lyrics.Parse("[00:10.00][00:20.00]Ulang");

        Assert.Equal(2, lines.Count);
        Assert.Equal("Ulang", lines[0].Text);
        Assert.Equal("Ulang", lines[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(20), lines[1].At);
    }

    [Fact]
    public void SkipsMetadataAndUntimedLines()
    {
        var lines = Lyrics.Parse("""
            [ar:Artis]
            [ti:Judul]
            [00:01.00]Isi
            tanpa tag
            """);

        Assert.Single(lines);
        Assert.Equal("Isi", lines[0].Text);
    }

    [Fact]
    public void SortsOutOfOrderLines()
    {
        var lines = Lyrics.Parse("[00:30]Ketiga\n[00:10]Pertama\n[00:20]Kedua");

        Assert.Equal(new[] { "Pertama", "Kedua", "Ketiga" }, lines.Select(line => line.Text));
    }

    [Theory]
    [InlineData("[00:12.3]x", 12300)]
    [InlineData("[00:12.34]x", 12340)]
    [InlineData("[00:12.345]x", 12345)]
    [InlineData("[00:12]x", 12000)]
    public void FractionDigitsAreMilliseconds(string lrc, int expected)
    {
        var line = Assert.Single(Lyrics.Parse(lrc));

        Assert.Equal(expected, line.At.TotalMilliseconds);
    }

    [Fact]
    public void CurrentLineFollowsPosition()
    {
        var lines = Lyrics.Parse("[00:10]Satu\n[00:20]Dua");

        Assert.Null(Lyrics.At(lines, TimeSpan.FromSeconds(5)));
        Assert.Equal("Satu", Lyrics.At(lines, TimeSpan.FromSeconds(10)));
        Assert.Equal("Satu", Lyrics.At(lines, TimeSpan.FromSeconds(19.9)));
        Assert.Equal("Dua", Lyrics.At(lines, TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public void EmptyOrBrokenInputGivesNothing()
    {
        Assert.Empty(Lyrics.Parse(null));
        Assert.Empty(Lyrics.Parse("   "));
        Assert.Empty(Lyrics.Parse("lirik tanpa waktu sama sekali"));
        Assert.Null(Lyrics.At(Array.Empty<LyricLine>(), TimeSpan.FromSeconds(1)));
    }
}
