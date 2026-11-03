using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class LyricsLibraryTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "island-lyrics-" + Guid.NewGuid().ToString("N"));

    public LyricsLibraryTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(folder, name), content);

    [Fact]
    public void ReadsTheArtistAndTitleFile()
    {
        Write("Nadin Amizah - Rumpang.lrc", "[00:01]Satu\n[00:05]Dua");

        var lines = LyricsLibrary.Load(folder, "Nadin Amizah", "Rumpang");

        Assert.Equal(2, lines.Count);
        Assert.Equal("Dua", Lyrics.At(lines, TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void FallsBackToTitleOnly()
    {
        Write("Rumpang.lrc", "[00:02]Hanya judul");

        var lines = LyricsLibrary.Load(folder, "Artis Lain", "Rumpang");

        Assert.Equal("Hanya judul", Assert.Single(lines).Text);
    }

    [Fact]
    public void MatchingIgnoresCase()
    {
        Write("NADIN AMIZAH - RUMPANG.LRC", "[00:03]Huruf besar");

        Assert.Single(LyricsLibrary.Load(folder, "nadin amizah", "rumpang"));
    }

    [Fact]
    public void MissingOrEmptyFilesGiveNothing()
    {
        Write("Rumpang.lrc", "  ");
        Write("Kosong.lrc", "[ar:tanpa lirik]");

        Assert.Empty(LyricsLibrary.Load(folder, "Nadin", "Rumpang"));
        Assert.Empty(LyricsLibrary.Load(folder, "Nadin", "Kosong"));
        Assert.Empty(LyricsLibrary.Load(folder, "Nadin", "Tidak Ada"));
        Assert.Empty(LyricsLibrary.Load(Path.Combine(folder, "tidak-ada"), "Nadin", "Rumpang"));
    }

    [Fact]
    public void UnsafeTitlesStillFindTheirFile()
    {
        Write("AC_DC - Back_.lrc", "[00:01]Judul aneh");

        Assert.Single(LyricsLibrary.Load(folder, "AC/DC", "Back?"));
    }
}
