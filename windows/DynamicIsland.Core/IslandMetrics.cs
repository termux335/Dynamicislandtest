namespace DynamicIsland.Core;

/// <summary>Ukuran kapsul per state, mengikuti <c>shared/SPEC.md</c> bagian 5.</summary>
public readonly record struct CapsuleSize(double Width, double Height, double CornerRadius, double TopInset);

public static class IslandMetrics
{
    /// <summary>Jarak mengambang dari tepi atas layar (Windows tidak punya notch).</summary>
    public const double FloatingTopInset = 8;

    /// <summary>
    /// Ukuran expanded, sama untuk semua jenis aktivitas. Kartu dibuat lebih
    /// kecil dari wireframe awal (560 x 176) supaya tidak menutupi konten di
    /// layar kecil; isi kartu harus tetap muat tanpa terpotong.
    /// </summary>
    public static readonly CapsuleSize Expanded = new(480, 152, 28, 0);

    /// <summary>Ukuran compact bergantung pada jenis aktivitas utama.</summary>
    public static CapsuleSize CompactFor(ActivityKind kind, DisplayMode mode = DisplayMode.Standar) => kind switch
    {
        ActivityKind.Timer => mode == DisplayMode.Standar
            ? new CapsuleSize(260, 36, 18, 0)
            : new CapsuleSize(220, 32, 16, 0),
        ActivityKind.Call => mode == DisplayMode.Standar
            ? new CapsuleSize(300, 36, 18, 0)
            : new CapsuleSize(252, 32, 16, 0),
        _ => mode == DisplayMode.Standar
            ? new CapsuleSize(200, 34, 17, 0)
            : new CapsuleSize(168, 30, 15, 0),
    };

    public static readonly CapsuleSize Minimal = new(34, 34, 17, 0);

    public static readonly CapsuleSize MinimalRingkas = new(30, 30, 15, 0);

    public static readonly CapsuleSize ExpandedNotification = new(312, 84, 28, 0);

    public static readonly CapsuleSize ExpandedNotificationRingkas = new(272, 72, 24, 0);

    public static readonly CapsuleSize ExpandedRingkas = new(400, 128, 24, 0);

    public static readonly CapsuleSize Shelf = new(312, 84, 20, 0);

    public static readonly CapsuleSize QuickActions = new(312, 64, 20, 0);

    /// <summary>Jarak antara kapsul utama dan kapsul minimal kedua (Tahap 3).</summary>
    public const double SecondaryGap = 8;

    /// <summary>Kapsul kedua selalu minimal, jadi mengikuti ukuran idle.</summary>
    public static CapsuleSize SecondaryFor(DisplayMode mode = DisplayMode.Standar) => Idle(mode);

    /// <summary>Ukuran kapsul saat tidak ada aktivitas (lingkaran kecil).</summary>
    public static CapsuleSize Idle(DisplayMode mode = DisplayMode.Standar) =>
        mode == DisplayMode.Standar ? Minimal : MinimalRingkas;

    /// <summary>Ukuran untuk aktivitas pada state tertentu dan mode tampilan.</summary>
    public static CapsuleSize For(Activity activity, DisplayMode mode = DisplayMode.Standar)
    {
        var minimal = Idle(mode);
        var expanded = mode == DisplayMode.Standar ? Expanded : ExpandedRingkas;
        var expandedNotification = mode == DisplayMode.Standar ? ExpandedNotification : ExpandedNotificationRingkas;

        return activity.State switch
        {
            ActivityState.Minimal => minimal,
            ActivityState.Compact => CompactFor(activity.Kind, mode),
            _ => activity.Kind == ActivityKind.Notification ? expandedNotification : expanded,
        };
    }
}

public static class IslandPalette
{
    /// <summary>Latar hitam murni.</summary>
    public const uint Background = 0x000000;

    public const uint TextPrimary = 0xFFFFFF;

    /// <summary>Abu terang untuk teks sekunder.</summary>
    public const uint TextSecondary = 0xB0B0B0;

    /// <summary>Satu-satunya aksen.</summary>
    public const uint Accent = 0xFF9F43;
}
