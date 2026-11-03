namespace DynamicIsland.Core;

/// <summary>
/// Mode tampilan kapsul (Tahap 2).
///
/// <see cref="Standar"/> memakai ukuran di <c>shared/SPEC.md</c> bagian 5.
/// <see cref="Ringkas"/> memakai ukuran lebih kecil dengan isi yang sama.
/// </summary>
public enum DisplayMode
{
    Standar,
    Ringkas,
}

public static class DisplayModeExtensions
{
    /// <summary>Nama mode seperti yang dipakai di baris perintah dan log.</summary>
    public static string ToWireName(this DisplayMode mode) => mode.ToString().ToLowerInvariant();

    public static bool TryParseWireName(string? name, out DisplayMode mode)
    {
        foreach (var candidate in Enum.GetValues<DisplayMode>())
        {
            if (string.Equals(candidate.ToWireName(), name, StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }

        mode = default;
        return false;
    }
}
