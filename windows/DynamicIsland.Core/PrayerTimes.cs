using System.Globalization;

namespace DynamicIsland.Core;

/// <summary>Waktu salat yang dihitung lokal, tanpa jaringan (Tahap 4).</summary>
public enum Prayer
{
    Fajr,
    Sunrise,
    Dhuhr,
    Asr,
    Maghrib,
    Isha,
}

public readonly record struct PrayerTime(Prayer Prayer, DateTimeOffset At);

/// <summary>Koordinat dan zona waktu untuk perhitungan salat.</summary>
public sealed record PrayerLocation(double Latitude, double Longitude, TimeSpan UtcOffset)
{
    public static PrayerLocation Jakarta { get; } = new(-6.2088, 106.8456, TimeSpan.FromHours(7));
}

/// <summary>
/// Perhitungan waktu salat dengan posisi matahari (metode sudut Kemenag:
/// Subuh -20°, Isya -18°, Asar bayangan 1x). Semua aritmetika lokal.
/// </summary>
public static class PrayerCalculator
{
    public static IReadOnlyList<PrayerTime> For(DateOnly date, PrayerLocation location)
    {
        var latitude = location.Latitude * Math.PI / 180;
        var dayOfYear = date.DayOfYear;
        var gamma = 2 * Math.PI / 365.0 * (dayOfYear - 1);

        var equationOfTime = 229.18 * (
            0.000075 +
            (0.001868 * Math.Cos(gamma)) -
            (0.032077 * Math.Sin(gamma)) -
            (0.014615 * Math.Cos(2 * gamma)) -
            (0.040849 * Math.Sin(2 * gamma)));

        var declination =
            0.006918 -
            (0.399912 * Math.Cos(gamma)) +
            (0.070257 * Math.Sin(gamma)) -
            (0.006758 * Math.Cos(2 * gamma)) +
            (0.000907 * Math.Sin(2 * gamma)) -
            (0.002697 * Math.Cos(3 * gamma)) +
            (0.00148 * Math.Sin(3 * gamma));

        var solarNoon = 720 - (4 * location.Longitude) - equationOfTime + location.UtcOffset.TotalMinutes;
        var midnight = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), location.UtcOffset);

        var times = new List<PrayerTime>();

        double? Offset(double altitude) => HourOffset(altitude, latitude, declination);

        void Add(Prayer prayer, double? minutes)
        {
            if (minutes is { } value && !double.IsNaN(value)) times.Add(new PrayerTime(prayer, midnight.AddMinutes(Math.Round(value))));
        }

        Add(Prayer.Fajr, Offset(-20) is { } fajr ? solarNoon - fajr : null);
        Add(Prayer.Sunrise, Offset(-0.833) is { } rise ? solarNoon - rise : null);
        Add(Prayer.Dhuhr, solarNoon + 1);
        Add(Prayer.Asr, Offset(AsrAltitude(latitude, declination)) is { } asr ? solarNoon + asr : null);
        Add(Prayer.Maghrib, Offset(-0.833) is { } set ? solarNoon + set : null);
        Add(Prayer.Isha, Offset(-18) is { } isha ? solarNoon + isha : null);

        return times.OrderBy(time => time.At).ToList();
    }

    /// <summary>Ketinggian matahari untuk Asar: bayangan sama panjang dengan tinggi objek.</summary>
    private static double AsrAltitude(double latitude, double declination)
    {
        var angle = Math.Abs(latitude - declination);
        return Math.Atan(1 / (1 + Math.Tan(angle))) * 180 / Math.PI;
    }

    /// <summary>Selisih menit dari tengah hari untuk satu ketinggian matahari.</summary>
    private static double? HourOffset(double altitudeDegrees, double latitude, double declination)
    {
        var altitude = altitudeDegrees * Math.PI / 180;
        var denominator = Math.Cos(latitude) * Math.Cos(declination);
        if (Math.Abs(denominator) < 1e-9) return null;

        var cosine = (Math.Sin(altitude) - (Math.Sin(latitude) * Math.Sin(declination))) / denominator;
        if (cosine is < -1 or > 1) return null;

        var hourAngle = Math.Acos(cosine) * 180 / Math.PI;
        return 4 * hourAngle;
    }
}

/// <summary>Hitung mundur ke salat berikutnya.</summary>
public static class PrayerPlan
{
    public const string ActivityId = "prayer";

    public static PrayerTime? Next(IReadOnlyList<PrayerTime> times, DateTimeOffset now) =>
        times.Where(time => time.At > now).OrderBy(time => time.At).Select(time => (PrayerTime?)time).FirstOrDefault();

    public static Activity? ActivityFor(PrayerTime next, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;
        var left = next.At - now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;

        return new Activity(
            id: ActivityId,
            kind: ActivityKind.Calendar,
            priority: 42,
            title: strings.Get("prayer." + next.Prayer.ToString().ToLowerInvariant()),
            subtitle: strings.Format("prayer.in", DurationLabel(left, strings)),
            isLive: true);
    }

    private static string DurationLabel(TimeSpan left, IslandText text)
    {
        if (left.TotalHours >= 1)
        {
            return text.Format("duration.hoursMinutes", (int)left.TotalHours, left.Minutes);
        }

        if (left.TotalMinutes >= 1)
        {
            return text.Format("duration.minutes", (int)left.TotalMinutes);
        }

        return text.Format(
            "duration.seconds",
            Math.Max(0, left.Seconds).ToString(CultureInfo.InvariantCulture));
    }
}
