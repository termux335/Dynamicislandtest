using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class Stage5BTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(7));

    // --- Tunda notifikasi saat mengetik ---

    [Fact]
    public void TypingHoldsNotificationsWhileInputIsFresh()
    {
        Assert.True(TypingRules.ShouldHold(TimeSpan.FromMilliseconds(300)));
        Assert.False(TypingRules.ShouldHold(TimeSpan.FromSeconds(3)));
        Assert.False(TypingRules.ShouldHold(TimeSpan.Zero, enabled: false));
    }

    [Fact]
    public void TypingReleasesAfterARealPause()
    {
        Assert.False(TypingRules.ShouldRelease(TimeSpan.FromMilliseconds(500)));
        Assert.True(TypingRules.ShouldRelease(TimeSpan.FromMilliseconds(1600)));
        Assert.True(TypingRules.ShouldRelease(TimeSpan.Zero, enabled: false));
    }

    // --- Jam tenang ---

    [Fact]
    public void QuietHoursWrapAroundMidnight()
    {
        Assert.True(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 23, 0, 0, TimeSpan.Zero), true, 22, 7));
        Assert.True(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.Zero), true, 22, 7));
        Assert.False(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), true, 22, 7));
        Assert.False(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 23, 0, 0, TimeSpan.Zero), false, 22, 7));
    }

    [Fact]
    public void QuietHoursWorkDuringTheDayToo()
    {
        Assert.True(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 13, 0, 0, TimeSpan.Zero), true, 12, 14));
        Assert.False(QuietHoursRules.IsQuiet(new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero), true, 12, 14));
    }

    [Fact]
    public void QuietHoursFilterNonCriticalActivities()
    {
        var controller = new IslandController();
        controller.Refresh(new Activity("notif", ActivityKind.Notification, priority: 90, title: "Pesan"));

        controller.SetQuietHours(true);

        Assert.Null(controller.Presentation.Primary);
        Assert.Equal(0.65, controller.Presentation.Opacity);
        Assert.True(controller.Presentation.QuietHours);

        controller.Refresh(new Activity("call", ActivityKind.Call, title: "Rapat"));

        Assert.Equal("call", controller.Presentation.Primary?.Id);
    }

    [Fact]
    public void EndingQuietHoursRestoresTheCapsule()
    {
        var controller = new IslandController();
        controller.Refresh(new Activity("media", ActivityKind.Media, title: "Lagu"));
        controller.SetQuietHours(true);

        controller.SetQuietHours(false);

        Assert.Equal(1, controller.Presentation.Opacity);
        Assert.Equal("media", controller.Presentation.Primary?.Id);
    }

    // --- Musik fokus ---

    [Fact]
    public void FocusMusicOnlyStartsForAnExistingTarget()
    {
        Assert.False(FocusMusicRules.CanPlay(null));
        Assert.False(FocusMusicRules.CanPlay("   "));
        Assert.False(FocusMusicRules.CanPlay(@"C:\tidak-ada\playlist.m3u"));
        Assert.True(FocusMusicRules.CanPlay("https://music.example.com/fokus"));

        Assert.False(FocusMusicRules.ShouldStart(false, "https://music.example.com/fokus"));
        Assert.True(FocusMusicRules.ShouldStart(true, "https://music.example.com/fokus"));
    }

    // --- Catatan dan snippet ---

    [Fact]
    public void NotesAndSnippetsSurviveARoundTrip()
    {
        var folder = Path.Combine(Path.GetTempPath(), "island-notes-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "notes.json");

        try
        {
            var store = new NoteStore(file);
            store.Add("Beli susu", NoteKind.Note, Now);
            store.Add("Alamat kantor: Jl. Contoh 1", NoteKind.Snippet, Now.AddMinutes(1));
            Assert.True(store.Save());

            var loaded = new NoteStore(file);
            Assert.True(loaded.Load());

            Assert.Equal(2, loaded.All.Count);
            Assert.Equal("Alamat kantor: Jl. Contoh 1", loaded.All[0].Text);
            Assert.Equal(NoteKind.Snippet, loaded.All[0].Kind);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void NoteSearchIsCaseInsensitive()
    {
        var store = new NoteStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "notes.json"));
        store.Add("Rapat produk", at: Now);

        Assert.Single(store.Search("RAPAT"));
        Assert.Empty(store.Search("tidak ada"));
    }

    [Fact]
    public void EmptyNotesAreIgnoredAndDuplicatesMoveToTheFront()
    {
        var store = new NoteStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "notes.json"));
        Assert.Null(store.Add("   "));

        store.Add("satu", at: Now);
        store.Add("dua", at: Now);
        store.Add("satu", at: Now);

        Assert.Equal(2, store.All.Count);
        Assert.Equal("satu", store.All[0].Text);
    }

    [Fact]
    public void DueRemindersAreReturned()
    {
        var store = new NoteStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "notes.json"));
        store.Add("minum", at: Now, remindAt: Now.AddMinutes(10));
        store.Add("rapat", at: Now, remindAt: Now.AddHours(2));

        Assert.Single(store.Due(Now.AddMinutes(11)));
        Assert.Equal("minum", store.Due(Now.AddMinutes(11))[0].Text);
    }

    // --- Catatan suara ---

    [Fact]
    public void RelativeReminderIsParsed()
    {
        var result = VoiceNotes.Parse("ingatkan 10 menit minum air", Now);

        Assert.Equal("minum air", result.Text);
        Assert.Equal(TimeSpan.FromMinutes(10), result.ReminderAfter);
        Assert.Equal(Now.AddMinutes(10), result.RemindAt);
        Assert.True(result.HasReminder);
    }

    [Fact]
    public void HourReminderIsParsed()
    {
        var result = VoiceNotes.Parse("remind me 2 hours stretch", Now);

        Assert.Equal("stretch", result.Text);
        Assert.Equal(TimeSpan.FromHours(2), result.ReminderAfter);
    }

    [Fact]
    public void ClockReminderMovesToTomorrowWhenAlreadyPassed()
    {
        var result = VoiceNotes.Parse("ingatkan pukul 08:30 rapat", Now);

        Assert.NotNull(result.RemindAt);
        Assert.Equal(Now.AddDays(1).Date.AddHours(8).AddMinutes(30), result.RemindAt!.Value.DateTime);
        Assert.Equal("rapat", result.Text);
    }

    [Fact]
    public void PlainSpeechBecomesANote()
    {
        var result = VoiceNotes.Parse("catat beli susu", Now);

        Assert.Equal("beli susu", result.Text);
        Assert.False(result.HasReminder);
    }

    [Fact]
    public void EmptySpeechIsIgnored()
    {
        Assert.Equal(string.Empty, VoiceNotes.Parse("   ", Now).Text);
    }

    // --- Aksesibilitas dan warna sampul ---

    [Fact]
    public void LargeTextScalesTheCapsule()
    {
        Assert.Equal(1, new AccessibilitySettings().TextScale);
        Assert.Equal(1.25, new AccessibilitySettings(LargeText: true).TextScale);
    }

    [Fact]
    public void AlbumColorPicksTheDominantSaturatedColor()
    {
        var pixels = new byte[8 * 8 * 4];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0xE0;     // blue
            pixels[index + 1] = 0x40; // green
            pixels[index + 2] = 0x20; // red
            pixels[index + 3] = 0xFF;
        }

        var color = AlbumColor.Choose(pixels, 8, 8);

        Assert.NotEqual(AlbumColor.Fallback, color);
        Assert.True((color & 0xFF) > ((color >> 16) & 0xFF));
    }

    [Fact]
    public void Stage5BDefaultsKeepNewModulesOnAndAlbumColorOff()
    {
        var settings = new Stage5BSettings();

        Assert.False(settings.AlbumColor);
        Assert.True(settings.Visualizer);
        Assert.Equal(22, settings.QuietStartHour);
        Assert.Equal(7, settings.QuietEndHour);
    }

    [Fact]
    public void AlbumColorFallsBackForGrayOrEmptyArtwork()
    {
        var gray = new byte[4 * 4 * 4];
        Array.Fill(gray, (byte)128);

        Assert.Equal(AlbumColor.Fallback, AlbumColor.Choose(gray, 4, 4));
        Assert.Equal(AlbumColor.Fallback, AlbumColor.Choose(Array.Empty<byte>(), 0, 0));
    }
}
