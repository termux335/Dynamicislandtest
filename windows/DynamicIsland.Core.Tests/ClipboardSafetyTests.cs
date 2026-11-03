using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class ClipboardSafetyTests
{
    [Fact]
    public void ValidCardNumbersKeepOnlyTheLastFourDigits()
    {
        var (text, redacted) = ClipboardSafety.Sanitize("Kartu 4111 1111 1111 1111 ya", ClipboardKind.Text);

        Assert.True(redacted);
        Assert.Contains("•••• 1111", text);
        Assert.DoesNotContain("4111 1111", text);
    }

    [Fact]
    public void NumbersThatFailLuhnAreNotTreatedAsCards()
    {
        Assert.False(ClipboardSafety.PassesLuhn("1234 5678 9012 3456"));

        var (text, redacted) = ClipboardSafety.Sanitize("1234 5678 9012 3456", ClipboardKind.Text);

        Assert.False(redacted);
        Assert.Equal("1234 5678 9012 3456", text);
    }

    [Fact]
    public void LabeledPasswordsAndTokensAreMaskedCompletely()
    {
        Assert.Equal(ClipboardSafety.Mask, ClipboardSafety.Sanitize("password: hunter2", ClipboardKind.Text).Text);
        Assert.Equal(ClipboardSafety.Mask, ClipboardSafety.Sanitize("api_key = sk-abc123", ClipboardKind.Text).Text);
        Assert.Equal(ClipboardSafety.Mask, ClipboardSafety.Sanitize("otp: 123456", ClipboardKind.Text).Text);
    }

    [Fact]
    public void JwtIsMasked()
    {
        var token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abcdefghijklmnop";

        Assert.True(ClipboardSafety.IsSensitive(token));
        Assert.Equal(ClipboardSafety.Mask, ClipboardSafety.Sanitize(token, ClipboardKind.Text).Text);
    }

    [Fact]
    public void NormalTextAndLinksAreKept()
    {
        Assert.Equal("Belanja besok", ClipboardSafety.Sanitize("Belanja besok", ClipboardKind.Text).Text);
        Assert.False(ClipboardSafety.Sanitize("Belanja besok", ClipboardKind.Text).Redacted);

        var url = "https://example.com/artikel?utm=1";
        Assert.Equal(url, ClipboardSafety.Sanitize(url, ClipboardKind.Text).Text);
    }

    [Fact]
    public void HistoryMasksBeforeStoringAndIgnoresConsecutiveDuplicates()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var history = new ClipboardHistory(now: () => now);

        var item = history.Add(ClipboardKind.Text, "password: hunter2");

        Assert.NotNull(item);
        Assert.True(item!.Redacted);
        Assert.Equal(ClipboardSafety.Mask, history.Items[0].Text);
        Assert.Null(history.Add(ClipboardKind.Text, "password: hunter2"));
    }

    [Fact]
    public void HistoryDropsTheOldestEntriesAtCapacity()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var history = new ClipboardHistory(2, now: () => now);

        history.Add(ClipboardKind.Text, "satu");
        history.Add(ClipboardKind.Text, "dua");
        history.Add(ClipboardKind.Text, "tiga");

        Assert.Equal(new[] { "tiga", "dua" }, history.Items.Select(item => item.Text));
    }

    [Fact]
    public void HistoryPurgesEntriesOlderThanRetention()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var history = new ClipboardHistory(retention: TimeSpan.FromHours(1), now: () => now);

        history.Add(ClipboardKind.Text, "lama", now);
        Assert.Single(history.Items);

        var later = now.AddHours(2);
        Assert.Equal(1, history.Purge(later));
        Assert.Empty(history.Items);
    }

    [Fact]
    public void SearchIsCaseInsensitiveAndLimitIsRespected()
    {
        var now = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var history = new ClipboardHistory(now: () => now);

        history.Add(ClipboardKind.Text, "Rapat Produk");
        history.Add(ClipboardKind.Text, "catatan rapat");

        Assert.Equal(2, history.Search("RAPAT").Count);
        Assert.Single(history.Search("rapat", limit: 1));
    }

    [Fact]
    public void ClearRemovesEverything()
    {
        var history = new ClipboardHistory();
        history.Add(ClipboardKind.Text, "sesuatu");

        history.Clear();

        Assert.Empty(history.Items);
    }
}
