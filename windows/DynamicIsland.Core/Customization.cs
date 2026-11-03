namespace DynamicIsland.Core;

/// <summary>
/// Aksen warna kapsul (Tahap 3: kustomisasi). Bawaan tetap oranye sesuai
/// <c>shared/SPEC.md</c>; pilihan lain tetap terang supaya kontras dengan latar
/// hitam tidak turun di bawah 4.5:1.
/// </summary>
public enum AccentChoice
{
    Oranye,
    Biru,
    Ungu,
    Hijau,
}

public static class AccentPalette
{
    /// <summary>Warna RGB (0xRRGGBB) untuk satu pilihan aksen.</summary>
    public static uint Rgb(this AccentChoice choice) => choice switch
    {
        AccentChoice.Biru => 0x4DA3FF,
        AccentChoice.Ungu => 0xB07BFF,
        AccentChoice.Hijau => 0x4CD97B,
        _ => IslandPalette.Accent,
    };

    public static string ToWireName(this AccentChoice choice) => choice.ToString().ToLowerInvariant();

    public static bool TryParseWireName(string? name, out AccentChoice choice)
    {
        foreach (var candidate in Enum.GetValues<AccentChoice>())
        {
            if (string.Equals(candidate.ToWireName(), name, StringComparison.OrdinalIgnoreCase))
            {
                choice = candidate;
                return true;
            }
        }

        choice = default;
        return false;
    }
}

/// <summary>Posisi tenang yang dipilih pengguna; penempatan adaptif tetap bisa menimpanya.</summary>
public enum PositionChoice
{
    TopCenter,
    TopLeft,
    TopRight,
}

public static class PositionChoices
{
    public static PlacementAnchor ToAnchor(this PositionChoice choice) => choice switch
    {
        PositionChoice.TopLeft => PlacementAnchor.TopLeft,
        PositionChoice.TopRight => PlacementAnchor.TopRight,
        _ => PlacementAnchor.TopCenter,
    };
}

/// <summary>
/// Preset prioritas (Tahap 3: kustomisasi). Pengguna bisa memilih jenis aktivitas
/// yang selalu jadi utama tanpa mengubah prioritas jenis lain.
/// </summary>
public enum PriorityPreset
{
    Bawaan,
    MediaUtama,
    TimerUtama,
}

public static class PriorityRules
{
    /// <summary>Prioritas untuk jenis yang dipilih pengguna: di atas media (70) dan timer (80).</summary>
    public const int BoostedPriority = 95;

    public static int PriorityFor(ActivityKind kind, int fallback, PriorityPreset preset) => preset switch
    {
        PriorityPreset.MediaUtama when kind == ActivityKind.Media => Math.Max(fallback, BoostedPriority),
        PriorityPreset.TimerUtama when kind == ActivityKind.Timer => Math.Max(fallback, BoostedPriority),
        _ => fallback,
    };

    public static string ToWireName(this PriorityPreset preset) => preset.ToString().ToLowerInvariant();

    public static bool TryParseWireName(string? name, out PriorityPreset preset)
    {
        foreach (var candidate in Enum.GetValues<PriorityPreset>())
        {
            if (string.Equals(candidate.ToWireName(), name, StringComparison.OrdinalIgnoreCase))
            {
                preset = candidate;
                return true;
            }
        }

        preset = default;
        return false;
    }
}
