namespace DynamicIsland.Core;

/// <summary>
/// Preferensi aksesibilitas Tahap 5b. Pembaca layar tetap mendapat teks serius;
/// dua saklar ini hanya mengubah tampilan dan suara.
/// </summary>
public sealed record AccessibilitySettings(
    bool SpeakNotifications = false,
    bool HighContrast = false,
    bool LargeText = false)
{
    /// <summary>Skala teks kapsul saat mode teks besar aktif.</summary>
    public double TextScale => LargeText ? 1.25 : 1;

    public double CapsuleScale => TextScale;
}

/// <summary>
/// Memilih warna aksen dari sampul album (Tahap 5b).
///
/// Sampel diambil dari piksel BGRA yang sudah didekode App. Piksel gelap, terlalu
/// terang, atau abu-abu dilewati; warna paling "berwarna" menang. Bila tidak ada
/// yang layak, aksen bawaan dipakai supaya kontras tetap aman.
/// </summary>
public static class AlbumColor
{
    public static uint Fallback => IslandPalette.Accent;

    public static uint Choose(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4) return Fallback;

        var buckets = new Dictionary<int, int>();
        var step = Math.Max(1, Math.Min(width, height) / 48);

        for (var y = 0; y < height; y += step)
        {
            for (var x = 0; x < width; x += step)
            {
                var index = ((y * width) + x) * 4;

                var blue = bgra[index];
                var green = bgra[index + 1];
                var red = bgra[index + 2];
                var alpha = bgra[index + 3];

                if (alpha < 128) continue;

                var sum = red + green + blue;
                if (sum < 90 || sum > 690) continue;

                var maximum = Math.Max(red, Math.Max(green, blue));
                var minimum = Math.Min(red, Math.Min(green, blue));
                var saturation = maximum - minimum;
                if (saturation < 24) continue;

                var key = ((red >> 5) << 10) | ((green >> 5) << 5) | (blue >> 5);
                buckets[key] = buckets.GetValueOrDefault(key) + saturation;
            }
        }

        if (buckets.Count == 0) return Fallback;

        var best = buckets.OrderByDescending(pair => pair.Value).First().Key;

        var resultRed = ((best >> 10) & 0x1F) << 3;
        var resultGreen = ((best >> 5) & 0x1F) << 3;
        var resultBlue = (best & 0x1F) << 3;

        return (uint)((resultRed << 16) | (resultGreen << 8) | resultBlue);
    }
}
