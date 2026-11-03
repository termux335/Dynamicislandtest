using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

/// <summary>Tes aturan baterai dan perangkat terhubung (Tahap 2).</summary>
public class AlertsTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstBatterySampleIsOnlyABaseline()
    {
        var rules = new BatteryRules(() => Start);

        Assert.Null(rules.OnSample(new BatteryState(8, Charging: false)));
    }

    [Fact]
    public void DroppingIntoLowRangeAnnouncesOnce()
    {
        var rules = new BatteryRules(() => Start);
        rules.OnSample(new BatteryState(40, Charging: false));

        var alert = rules.OnSample(new BatteryState(12, Charging: false));

        Assert.NotNull(alert);
        Assert.Equal(BatteryAlert.Id, alert!.Id);
        Assert.Equal(ActivityKind.Battery, alert.Kind);
        Assert.Equal("Baterai lemah", alert.Title);
        Assert.Equal("12% tersisa", alert.Subtitle);
        Assert.False(alert.IsLive);
        Assert.Equal(Start.AddMilliseconds(IslandEvent.DefaultTtlMs), alert.ExpiresAt);

        Assert.Null(rules.OnSample(new BatteryState(11, Charging: false)));
    }

    [Fact]
    public void LowThresholdIsFifteenPercentAndIgnoresCharging()
    {
        Assert.True(BatteryAlert.IsLow(15, charging: false));
        Assert.False(BatteryAlert.IsLow(16, charging: false));
        Assert.False(BatteryAlert.IsLow(5, charging: true));
    }

    [Fact]
    public void PluggingInAnnouncesChargingOnce()
    {
        var rules = new BatteryRules(() => Start);
        rules.OnSample(new BatteryState(50, Charging: false));

        var alert = rules.OnSample(new BatteryState(50, Charging: true));

        Assert.NotNull(alert);
        Assert.Equal("Mengisi daya", alert!.Title);
        Assert.Null(rules.OnSample(new BatteryState(50, Charging: true)));
    }

    [Fact]
    public void FirstDeviceSampleReportsTheCurrentList()
    {
        var rules = new DeviceRules(() => Start);

        var alert = rules.OnSample(new[] { "SSD 1TB", "  " });

        Assert.NotNull(alert);
        Assert.Equal(DeviceAlert.Id, alert!.Id);
        Assert.Equal("1 perangkat terhubung", alert.Title);
        Assert.Equal("SSD 1TB", alert.Subtitle);
        Assert.Equal(Start.AddMilliseconds(IslandEvent.DefaultTtlMs), alert.ExpiresAt);
    }

    [Fact]
    public void DeviceAlertOnlyFiresWhenTheListChanges()
    {
        var rules = new DeviceRules(() => Start);
        rules.OnSample(new[] { "SSD 1TB" });

        Assert.Null(rules.OnSample(new[] { "SSD 1TB" }));

        var alert = rules.OnSample(new[] { "SSD 1TB", "Flashdisk" });

        Assert.NotNull(alert);
        Assert.Equal("2 perangkat terhubung", alert!.Title);
        Assert.Equal("Flashdisk, SSD 1TB", alert.Subtitle);
    }

    [Fact]
    public void LongDeviceListsAreSummarized()
    {
        var alert = DeviceAlert.ActivityFor(new[] { "A", "B", "C", "D" }, Start);

        Assert.Equal("4 perangkat terhubung", alert.Title);
        Assert.Equal("A, B, +2 lainnya", alert.Subtitle);
    }

    [Fact]
    public void EmptyDeviceListSaysSo()
    {
        var alert = DeviceAlert.ActivityFor(Array.Empty<string>(), Start);

        Assert.Equal("Tidak ada perangkat terhubung", alert.Title);
        Assert.Null(alert.Subtitle);
    }
}
