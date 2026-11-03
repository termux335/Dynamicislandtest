namespace DynamicIsland.Core;

/// <summary>Kategori aplikasi yang sedang di depan layar.</summary>
public enum ForegroundCategory
{
    /// <summary>Aplikasi lain, desktop, atau tidak dikenali.</summary>
    Other,

    /// <summary>Game yang berjalan layar penuh.</summary>
    Game,

    /// <summary>Peramban: Chrome, Edge, Firefox, Brave, Opera, Vivaldi, Chromium.</summary>
    Browser,
}

/// <summary>
/// Info minimum tentang window depan yang dibutuhkan aturan penempatan.
/// Diisi lapisan UI dari window yang sedang fokus; core tidak menyentuh Win32.
/// </summary>
public sealed record ForegroundInfo(
    ForegroundCategory Category,
    bool IsFullscreen,
    bool IsMaximized,
    string? ProcessName = null);

/// <summary>Posisi kapsul di layar.</summary>
public enum PlacementAnchor
{
    /// <summary>Tengah atas, 8 px dari tepi atas (spesifikasi).</summary>
    TopCenter,

    /// <summary>Kiri tengah, 8 px dari tepi kiri: jauh dari bilah tab, alamat, dan HUD game.</summary>
    MidLeft,

    /// <summary>Kiri atas, 8 px dari kedua tepi (pilihan pengguna).</summary>
    TopLeft,

    /// <summary>Kanan atas, 8 px dari kedua tepi (pilihan pengguna).</summary>
    TopRight,
}

/// <summary>
/// Penempatan adaptif: kapsul pindah ke kiri tengah saat window depan memakai
/// seluruh layar dan memiliki bilah di atas (game layar penuh, peramban
/// maximized), supaya tidak menutupi tab, bilah alamat, atau HUD.
/// Browser layar penuh (F11) tetap di tengah atas karena tidak punya bilah.
/// </summary>
public static class PlacementRules
{
    /// <summary>Jarak kapsul dari tepi layar, sama untuk tengah atas dan kiri tengah.</summary>
    public const double EdgeInset = 8;

    /// <summary>
    /// Pilihan posisi untuk window depan sekarang. Tanpa konteks khusus, posisi
    /// tenang mengikuti pilihan pengguna (<paramref name="preferred"/>).
    /// </summary>
    public static PlacementAnchor Choose(
        ForegroundInfo? foreground,
        bool autoPlacement = true,
        PositionChoice preferred = PositionChoice.TopCenter)
    {
        if (!autoPlacement || foreground is null) return preferred.ToAnchor();

        return foreground switch
        {
            { Category: ForegroundCategory.Game, IsFullscreen: true } => PlacementAnchor.MidLeft,
            { Category: ForegroundCategory.Browser, IsMaximized: true } => PlacementAnchor.MidLeft,
            _ => preferred.ToAnchor(),
        };
    }
}
