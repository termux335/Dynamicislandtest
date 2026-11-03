namespace DynamicIsland.Core;

/// <summary>Tampilan pengganti OSD volume/kecerahan (Tahap 5a).</summary>
public enum OsdKind
{
    Volume,
    Brightness,
    Mute,
}

public sealed record OsdState(OsdKind Kind, int Percent, bool Muted = false);

/// <summary>
/// Membentuk alert OSD dari perubahan volume/kecerahan. OSD bawaan Windows tidak
/// bisa dimatikan tanpa hook, jadi modul ini boleh dinyalakan/dimatikan sendiri.
/// </summary>
public static class Osd
{
    public const string ActivityId = "osd";

    public static Activity ActivityFor(OsdState state, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        var title = state.Kind switch
        {
            OsdKind.Brightness => strings.Get("osd.brightness"),
            OsdKind.Mute => strings.Get("osd.mute"),
            _ => strings.Get("osd.volume"),
        };

        var subtitle = state.Muted ? strings.Get("osd.muted") : $"{Math.Clamp(state.Percent, 0, 100)}%";

        return new Activity(
            id: ActivityId,
            kind: ActivityKind.Device,
            priority: 88,
            title: title,
            subtitle: subtitle,
            progress: Math.Clamp(state.Percent, 0, 100) / 100.0,
            isLive: false,
            expiresAt: now.AddSeconds(2));
    }

    /// <summary>Hanya tampilkan saat nilainya benar-benar berubah.</summary>
    public static bool Changed(OsdState? previous, OsdState current) => previous != current;
}
