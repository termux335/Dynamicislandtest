namespace DynamicIsland.Core;

/// <summary>Tahap perlindungan layar OLED.</summary>
public enum OledPhase
{
    Active,
    Dimmed,
    Hidden,
}

public readonly record struct OledState(OledPhase Phase, double Opacity, bool Visible, int OffsetX, int OffsetY);

/// <summary>
/// Pelindung layar OLED (Tahap 4): geser posisi mikro berkala supaya piksel
/// tidak menampilkan konten sama persis terus-menerus, redup saat idle, dan
/// sembunyi setelah idle lama.
///
/// Semua keputusan dari durasi yang diberikan pemanggil, jadi bisa dites.
/// </summary>
public static class OledGuard
{
    /// <summary>Interval geser mikro.</summary>
    public static TimeSpan ShiftEvery { get; } = TimeSpan.FromMinutes(4);

    /// <summary>Idle sebelum kapsul diredupkan.</summary>
    public static TimeSpan DimAfter { get; } = TimeSpan.FromSeconds(60);

    /// <summary>Idle sebelum kapsul disembunyikan.</summary>
    public static TimeSpan HideAfter { get; } = TimeSpan.FromMinutes(10);

    /// <summary>Geseran 0 atau ±2 px; cukup untuk memindahkan beban piksel tepi.</summary>
    private static readonly (int X, int Y)[] Offsets =
    {
        (0, 0),
        (2, 0),
        (2, 2),
        (0, 2),
        (-2, 2),
        (-2, 0),
        (-2, -2),
        (0, -2),
    };

    public static (int X, int Y) Shift(TimeSpan sinceStart)
    {
        if (sinceStart < TimeSpan.Zero) sinceStart = TimeSpan.Zero;

        var step = (int)(sinceStart.Ticks / ShiftEvery.Ticks) % Offsets.Length;
        return Offsets[step];
    }

    public static OledState StateFor(TimeSpan idle, TimeSpan sinceStart, bool enabled = true)
    {
        if (!enabled) return new OledState(OledPhase.Active, 1, true, 0, 0);

        var (offsetX, offsetY) = Shift(sinceStart);

        if (idle >= HideAfter) return new OledState(OledPhase.Hidden, 0, false, offsetX, offsetY);
        if (idle >= DimAfter) return new OledState(OledPhase.Dimmed, 0.65, true, offsetX, offsetY);

        return new OledState(OledPhase.Active, 1, true, offsetX, offsetY);
    }
}
