using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class StringsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(Language.Id)]
    [InlineData(Language.En)]
    public void EveryKeyHasSeriousText(Language language)
    {
        Assert.Empty(StringCatalog.Load(language).MissingSerious);
    }

    [Fact]
    public void BothLanguagesCarryTheSameKeys()
    {
        var indonesian = StringCatalog.Load(Language.Id);
        var english = StringCatalog.Load(Language.En);

        Assert.Equal(indonesian.Count, english.Count);

        foreach (var key in indonesian.Keys)
        {
            Assert.True(english.Has(key), $"kunci {key} tidak ada di strings-english.json");
        }
    }

    [Fact]
    public void JokeyVariantIsUsedOnlyWhenRequested()
    {
        var catalog = StringCatalog.Load(Language.Id);

        Assert.Equal("Keluar", catalog.Get("menu.quit", HumorMode.Serius));
        Assert.Equal("Pamit", catalog.Get("menu.quit", HumorMode.Kocak));
    }

    [Fact]
    public void UnknownKeyIsReturnedAsIs()
    {
        Assert.Equal("tidak.ada", StringCatalog.Load(Language.Id).Get("tidak.ada"));
    }

    [Fact]
    public void MissingJokeyVariantFallsBackToSerious()
    {
        var catalog = StringCatalog.Parse(Language.Id, """{ "contoh": { "serius": "Serius" } }""");

        Assert.Equal("Serius", catalog.Get("contoh", HumorMode.Kocak));
        Assert.Contains("contoh", catalog.MissingKocak);
        Assert.Empty(catalog.MissingSerious);
    }

    [Fact]
    public void MissingSeriousTextIsReportedAndDoesNotInventCopy()
    {
        var catalog = StringCatalog.Parse(Language.Id, """{ "contoh": { "kocak": "Kocak" } }""");

        Assert.Contains("contoh", catalog.MissingSerious);
        Assert.Equal("contoh", catalog.Get("contoh", HumorMode.Serius));
    }

    [Fact]
    public void FormatFillsPlaceholders()
    {
        Assert.Equal("3 berkas di shelf", StringCatalog.Load(Language.Id).Format("shelf.count", HumorMode.Serius, 3));
    }

    [Fact]
    public void ScreenReaderAlwaysGetsSeriousText()
    {
        var text = new IslandText(Language.Id, HumorMode.Kocak);

        Assert.Equal("Pamit", text.Get("menu.quit"));
        Assert.Equal("Keluar", text.GetSerious("menu.quit"));
    }

    [Fact]
    public void BatteryAlertKeepsSeriousTitleAndAddsHumorInTheSubtitle()
    {
        var text = new IslandText(Language.Id, HumorMode.Kocak);
        var alert = BatteryAlert.Low(20, Now, text);

        Assert.Equal("Baterai lemah", alert.Title);
        Assert.Equal("20%. Colokin, kasihan dia.", alert.Subtitle);
    }

    [Fact]
    public void AlertsDefaultToSeriousIndonesian()
    {
        var alert = BatteryAlert.Charging(50, Now);

        Assert.Equal("Mengisi daya", alert.Title);
        Assert.Equal("50%", alert.Subtitle);
    }

    [Fact]
    public void AlertsFollowTheLanguage()
    {
        var text = new IslandText(Language.En, HumorMode.Serius);

        Assert.Equal("Timer finished", TimerAlert.Done(Now, text).Title);
        Assert.Equal("Charging", BatteryAlert.Charging(50, Now, text).Title);
        Assert.Equal("Low battery", BatteryAlert.Low(10, Now, text).Title);
    }

    [Fact]
    public void CalendarCountdownFollowsTheLanguage()
    {
        var item = new CalendarEvent(
            "rapat",
            "Rapat Produk",
            Now.AddMinutes(15),
            Now.AddMinutes(45));

        var english = new IslandText(Language.En, HumorMode.Serius);
        var indonesian = new IslandText(Language.Id, HumorMode.Serius);

        Assert.Equal("starts in 15 min", CalendarPlan.CountdownLabel(item, Now, english));
        Assert.Equal("mulai dalam 15 menit", CalendarPlan.CountdownLabel(item, Now, indonesian));
    }

    [Fact]
    public void TimerTextFollowsTheSelectedPersonality()
    {
        var engine = new TimerEngine(() => Now);
        engine.StartCountdown(TimeSpan.FromMinutes(25));

        var kocak = new IslandText(Language.Id, HumorMode.Kocak);
        Assert.Equal("Kerja", engine.Snapshot(kocak)!.Title);

        var serius = new IslandText(Language.Id, HumorMode.Serius);
        Assert.Equal("Fokus", engine.Snapshot(serius)!.Title);
    }

    [Fact]
    public void SwitchingLanguageAndHumorTakesEffectImmediately()
    {
        var text = new IslandText(Language.Id, HumorMode.Serius);

        text.Use(Language.En, HumorMode.Kocak);

        Assert.Equal(Language.En, text.Language);
        Assert.Equal(HumorMode.Kocak, text.Humor);
        Assert.Equal("Bow out", text.Get("menu.quit"));
    }
}
