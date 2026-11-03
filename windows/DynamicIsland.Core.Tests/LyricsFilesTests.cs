using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class LyricsFilesTests
{
    [Fact]
    public void PrefersArtistThenTitle()
    {
        var names = LyricsFiles.Candidates("Nadin Amizah", "Rumpang");

        Assert.Equal(new[] { "Nadin Amizah - Rumpang", "Rumpang" }, names);
    }

    [Fact]
    public void WithoutArtistOnlyTheTitleIsTried()
    {
        Assert.Equal(new[] { "Rumpang" }, LyricsFiles.Candidates(null, "Rumpang"));
        Assert.Equal(new[] { "Rumpang" }, LyricsFiles.Candidates("   ", "Rumpang"));
    }

    [Fact]
    public void UnsafeCharactersBecomeUnderscores()
    {
        var name = LyricsFiles.Safe("AC/DC: Back?");

        Assert.Equal("AC_DC_ Back_", name);
        Assert.DoesNotContain(name, Path.GetInvalidFileNameChars());
    }

    [Fact]
    public void EmptyTitleGivesNoCandidates()
    {
        Assert.Empty(LyricsFiles.Candidates("Artis", "  "));
        Assert.Equal(string.Empty, LyricsFiles.Safe(null));
    }
}
