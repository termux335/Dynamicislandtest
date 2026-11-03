using System.Globalization;

namespace DynamicIsland.Core;

/// <summary>Skenario otomatis (Tahap 4): island menyesuaikan diri dengan konteks.</summary>
public enum ScenarioKind
{
    None,
    Meeting,
    Study,
    NightCharging,
}

/// <summary>Masukan untuk memilih skenario.</summary>
public sealed record ScenarioInput(
    IslandContext Context = IslandContext.Normal,
    bool Charging = false,
    bool StudyMode = false,
    DateTimeOffset Now = default);

/// <summary>Rencana tampilan untuk satu skenario.</summary>
public sealed record ScenarioPlan(
    ScenarioKind Kind,
    bool SuppressNotifications = false,
    bool ShowsClock = false,
    ActivityKind? FocusKind = null)
{
    public static ScenarioPlan None { get; } = new(ScenarioKind.None);
}

public static class ScenarioRules
{
    /// <summary>Charging malam mulai jam ini.</summary>
    public const int NightStartHour = 22;

    /// <summary>Charging malam berakhir sebelum jam ini.</summary>
    public const int NightEndHour = 6;

    public const string NightActivityId = "scenario-night";

    /// <summary>
    /// Urutan: rapat menang (kerja), lalu mode belajar, lalu charging malam.
    /// Belajar menahan notifikasi non-kritis; rapat tidak, karena tombol bisu
    /// dan durasi harus tetap terlihat.
    /// </summary>
    public static ScenarioPlan Evaluate(ScenarioInput input)
    {
        if (input.Context == IslandContext.Meeting)
        {
            return new ScenarioPlan(ScenarioKind.Meeting, FocusKind: ActivityKind.Call);
        }

        if (input.StudyMode)
        {
            return new ScenarioPlan(ScenarioKind.Study, SuppressNotifications: true, FocusKind: ActivityKind.Timer);
        }

        if (input.Charging && IsNight(input.Now))
        {
            return new ScenarioPlan(ScenarioKind.NightCharging, ShowsClock: true);
        }

        return ScenarioPlan.None;
    }

    public static bool IsNight(DateTimeOffset at) =>
        at.Hour >= NightStartHour || at.Hour < NightEndHour;

    /// <summary>Kartu jam untuk charging malam; prioritas rendah supaya tidak merebut aktivitas lain.</summary>
    public static Activity NightClock(DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: NightActivityId,
            kind: ActivityKind.Battery,
            priority: 5,
            title: now.ToString("HH:mm", CultureInfo.InvariantCulture),
            subtitle: strings.Get("scenario.night.subtitle"),
            isLive: true);
    }
}
