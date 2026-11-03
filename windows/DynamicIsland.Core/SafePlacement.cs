namespace DynamicIsland.Core;

/// <summary>Posisi aman yang dicoba berurutan saat konteks memaksa pindah.</summary>
public enum SafePosition
{
    TopCenter,
    TopLeft,
    TopRight,
    BottomCenter,
}

/// <summary>Kotak di layar dalam piksel (bukan DIP): dipakai untuk zona terlarang.</summary>
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public double CenterX => Left + (Width / 2);

    public double CenterY => Top + (Height / 2);
}

/// <summary>
/// Zona yang tidak boleh ditutupi kapsul untuk satu aplikasi/game (mis. HUD).
/// <see cref="App"/> kosong berarti berlaku untuk semua aplikasi.
/// </summary>
public sealed record ForbiddenZone(string App, double Left, double Top, double Width, double Height)
{
    public ScreenArea Area => new(Left, Top, Width, Height);

    public bool Matches(string? process) =>
        string.IsNullOrWhiteSpace(App) || string.Equals(App, process, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Penempatan aman (Tahap 4): coba tengah atas, kiri atas, kanan atas, lalu
/// tengah bawah; lewati zona terlarang; geser kecil menjauhi kursor.
/// </summary>
public static class SafePlacement
{
    /// <summary>Jarak dari tepi layar, sama dengan kapsul normal.</summary>
    public const double EdgeInset = IslandMetrics.FloatingTopInset;

    /// <summary>Jarak kursor yang memicu geser (spesifikasi: sekitar 80 px).</summary>
    public const double CursorAvoidDistance = 80;

    /// <summary>Geser maksimum supaya kapsul tidak "kabur" dari layar.</summary>
    public const double CursorPushLimit = 48;

    /// <summary>Urutan percobaan posisi aman.</summary>
    public static IReadOnlyList<SafePosition> Order { get; } = new[]
    {
        SafePosition.TopCenter,
        SafePosition.TopLeft,
        SafePosition.TopRight,
        SafePosition.BottomCenter,
    };

    /// <summary>Koordinat kiri-atas untuk satu posisi pada satu area layar.</summary>
    public static (double Left, double Top) Spot(ScreenArea screen, double width, double height, SafePosition position)
    {
        var inset = EdgeInset;

        return position switch
        {
            SafePosition.TopLeft => (screen.Left + inset, screen.Top + inset),
            SafePosition.TopRight => (screen.Right - width - inset, screen.Top + inset),
            SafePosition.BottomCenter => (screen.Left + ((screen.Width - width) / 2), screen.Bottom - height - inset),
            _ => (screen.Left + ((screen.Width - width) / 2), screen.Top + inset),
        };
    }

    /// <summary>True bila dua kotak bersinggungan.</summary>
    public static bool Overlaps(ScreenArea first, ScreenArea second) =>
        first.Left < second.Right &&
        first.Right > second.Left &&
        first.Top < second.Bottom &&
        first.Bottom > second.Top;

    /// <summary>
    /// Posisi pertama yang tidak menutupi zona terlarang. Posisi pilihan pengguna
    /// dicoba lebih dulu. Bila semua zona terlarang, pilihan pengguna tetap dipakai
    /// (lebih baik terlihat daripada hilang).
    /// </summary>
    public static SafePosition Choose(
        ScreenArea screen,
        double width,
        double height,
        SafePosition preferred = SafePosition.TopCenter,
        IEnumerable<ForbiddenZone>? forbidden = null,
        string? process = null)
    {
        var zones = forbidden?
            .Where(zone => zone.Matches(process))
            .Select(zone => zone.Area)
            .ToList() ?? new List<ScreenArea>();

        var candidates = new List<SafePosition> { preferred };
        candidates.AddRange(Order.Where(position => position != preferred));

        foreach (var candidate in candidates)
        {
            var (left, top) = Spot(screen, width, height, candidate);
            var capsule = new ScreenArea(left, top, width, height);

            if (!zones.Any(zone => Overlaps(capsule, zone))) return candidate;
        }

        return preferred;
    }

    /// <summary>
    /// Geser mendatar menjauhi kursor bila jaraknya kurang dari 80 px. Kembali ke
    /// nol begitu kursor menjauh, jadi kapsul tidak berpindah permanen.
    /// </summary>
    public static (double Dx, double Dy) Dodge(
        ScreenArea capsule,
        double cursorX,
        double cursorY,
        double threshold = CursorAvoidDistance,
        double limit = CursorPushLimit)
    {
        if (capsule.Width <= 0 || capsule.Height <= 0) return (0, 0);

        var nearestX = Math.Clamp(cursorX, capsule.Left, capsule.Right);
        var nearestY = Math.Clamp(cursorY, capsule.Top, capsule.Bottom);
        var dx = cursorX - nearestX;
        var dy = cursorY - nearestY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance >= threshold) return (0, 0);

        var push = Math.Min(limit, threshold - distance + 8);
        var direction = capsule.CenterX >= cursorX ? 1 : -1;

        return (Math.Round(direction * push), 0);
    }
}
