namespace DynamicIsland.Core;

/// <summary>
/// Aturan Baterai dan Perangkat Terhubung (Tahap 2).
///
/// Keduanya Transient Alert (kind <see cref="ActivityKind.Battery"/> /
/// <see cref="ActivityKind.Device"/>), jadi hanya diumumkan saat terjadi
/// perubahan yang layak — bukan setiap pembaruan data, supaya tidak spam.
/// </summary>
public readonly record struct BatteryState(int Level, bool Charging);

/// <summary>
/// Mengubah seri sampel baterai menjadi alert.
/// Sampel pertama hanya jadi acuan, jadi perangkat yang sudah lemah saat app
/// mulai tidak langsung berbunyi.
/// </summary>
public sealed class BatteryRules
{
    private readonly Func<DateTimeOffset> now;
    private readonly IslandText text;
    private bool baselineTaken;
    private BatteryState? last;

    public BatteryRules(Func<DateTimeOffset>? now = null, IslandText? text = null)
    {
        this.now = now ?? (() => DateTimeOffset.Now);
        this.text = text ?? IslandText.Default;
    }

    /// <summary>Kembalikan alert bila ada transisi, atau null.</summary>
    public Activity? OnSample(BatteryState state)
    {
        var previous = last;
        last = state;

        if (!baselineTaken)
        {
            baselineTaken = true;
            return null;
        }

        var isLow = BatteryAlert.IsLow(state.Level, state.Charging);
        var wasLow = previous is { } previousState && BatteryAlert.IsLow(previousState.Level, previousState.Charging);

        return state switch
        {
            _ when isLow && !wasLow => BatteryAlert.Low(state.Level, now(), text),
            _ when state.Charging && previous?.Charging == false => BatteryAlert.Charging(state.Level, now(), text),
            _ => null,
        };
    }
}

/// <summary>Pembuat alert baterai.</summary>
public static class BatteryAlert
{
    /// <summary>Persen di bawah ini (dan tidak sedang mengisi) dianggap lemah.</summary>
    public const int LowThresholdPercent = 15;

    public const string Id = "battery";

    public static bool IsLow(int level, bool charging) => !charging && level <= LowThresholdPercent;

    public static Activity Low(int level, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: Id,
            kind: ActivityKind.Battery,
            title: strings.Get("alert.batteryLow.title"),
            subtitle: strings.Format("alert.batteryLow.subtitle", level),
            isLive: false,
            expiresAt: now.AddMilliseconds(IslandEvent.DefaultTtlMs));
    }

    public static Activity Charging(int level, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: Id,
            kind: ActivityKind.Battery,
            title: strings.Get("alert.batteryCharging.title"),
            subtitle: strings.Format("alert.batteryCharging.subtitle", level),
            isLive: false,
            expiresAt: now.AddMilliseconds(IslandEvent.DefaultTtlMs));
    }
}

/// <summary>
/// Mengubah seri daftar perangkat terhubung menjadi alert.
/// Hanya memberi alert saat daftarnya berubah.
/// </summary>
public sealed class DeviceRules
{
    private readonly Func<DateTimeOffset> now;
    private readonly bool announceFirstSample;
    private readonly IslandText text;
    private IReadOnlySet<string>? last;

    /// <param name="announceFirstSample">
    /// Bila false, sampel pertama hanya jadi acuan (dipakai monitor yang berjalan
    /// sejak app dibuka, supaya tidak mengumumkan daftar awal).
    /// </param>
    public DeviceRules(Func<DateTimeOffset>? now = null, bool announceFirstSample = true, IslandText? text = null)
    {
        this.now = now ?? (() => DateTimeOffset.Now);
        this.announceFirstSample = announceFirstSample;
        this.text = text ?? IslandText.Default;
    }

    public Activity? OnSample(IEnumerable<string> names)
    {
        var current = DeviceAlert.Normalize(names);
        var first = last is null;
        if (!first && current.SetEquals(last!)) return null;

        last = current;
        return first && !announceFirstSample ? null : DeviceAlert.ActivityFor(current, now(), text);
    }
}

/// <summary>Pembuat alert perangkat terhubung.</summary>
public static class DeviceAlert
{
    public const string Id = "devices";

    /// <summary>Jumlah nama yang dicantumkan sebelum diganti ringkasan.</summary>
    private const int MaxNames = 2;

    public static IReadOnlySet<string> Normalize(IEnumerable<string> names) =>
        new SortedSet<string>(
            names.Select(name => name.Trim()).Where(name => name.Length > 0),
            StringComparer.CurrentCulture);

    public static Activity ActivityFor(IEnumerable<string> names, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        var sorted = names.Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .ToList();

        var shown = sorted.Take(MaxNames).ToList();
        var extra = sorted.Count - shown.Count;
        var parts = shown
            .Concat(extra > 0 ? new[] { strings.Format("alert.device.more", extra) } : Array.Empty<string>())
            .ToList();

        return new Activity(
            id: Id,
            kind: ActivityKind.Device,
            title: sorted.Count == 0
                ? strings.Get("alert.device.none")
                : strings.Format("alert.device.title", sorted.Count),
            subtitle: parts.Count > 0 ? string.Join(", ", parts) : null,
            isLive: false,
            expiresAt: now.AddMilliseconds(IslandEvent.DefaultTtlMs));
    }
}
