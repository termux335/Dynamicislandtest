using System.Globalization;
using System.Text.Json;

namespace DynamicIsland.Core;

/// <summary>Satu titik prakiraan cuaca BMKG.</summary>
public sealed record WeatherPoint(DateTimeOffset At, string Description, double? TemperatureC);

public sealed record WeatherWarning(string Description, DateTimeOffset At);

/// <summary>
/// Membaca prakiraan BMKG dan memilih peringatan cuaca terdekat (Tahap 4).
///
/// Parsing sengaja longgar: BMKG memakai <c>local_datetime</c> atau
/// <c>utc_datetime</c> dan struktur bersarang yang bisa berubah. Jaringan ada di
/// lapisan App; berkas ini murni parsing supaya bisa dites dengan contoh JSON.
/// </summary>
public static class BmkgWeather
{
    /// <summary>Frasa yang dianggap layak diperingatkan (bukan gerimis biasa).</summary>
    public static IReadOnlyList<string> SevereKeywords { get; } = new[]
    {
        "hujan lebat",
        "hujan petir",
        "hujan es",
        "badai",
        "petir",
        "angin kencang",
    };

    public static IReadOnlyList<WeatherPoint> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        var points = new List<WeatherPoint>();
        Walk(document.RootElement, points);

        return points.OrderBy(point => point.At).ToList();
    }

    /// <summary>Peringatan pertama dalam jendela waktu, atau null.</summary>
    public static WeatherWarning? Warning(
        IReadOnlyList<WeatherPoint> points,
        DateTimeOffset now,
        TimeSpan? horizon = null)
    {
        var limit = now + (horizon ?? TimeSpan.FromHours(6));

        foreach (var point in points.OrderBy(point => point.At))
        {
            if (point.At < now || point.At > limit) continue;
            if (IsSevere(point.Description)) return new WeatherWarning(point.Description, point.At);
        }

        return null;
    }

    public static bool IsSevere(string description)
    {
        var text = description.ToLowerInvariant();
        return SevereKeywords.Any(text.Contains);
    }

    /// <summary>Aktivitas singkat untuk peringatan cuaca.</summary>
    public static Activity ActivityFor(WeatherWarning warning, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: "weather",
            kind: ActivityKind.Notification,
            priority: 35,
            title: strings.Get("weather.warning.title"),
            subtitle: strings.Format(
                "weather.warning.subtitle",
                warning.Description,
                warning.At.ToString("HH:mm", CultureInfo.InvariantCulture)),
            isLive: false,
            expiresAt: now.AddSeconds(15));
    }

    private static void Walk(JsonElement element, List<WeatherPoint> points)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Walk(item, points);
                return;

            case JsonValueKind.Object:
                if (TryReadPoint(element, out var point)) points.Add(point);

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                    {
                        Walk(property.Value, points);
                    }
                }

                return;

            default:
                return;
        }
    }

    private static bool TryReadPoint(JsonElement element, out WeatherPoint point)
    {
        point = default!;

        if (!TryString(element, "weather_desc", out var description) || string.IsNullOrWhiteSpace(description)) return false;
        if (!TryDate(element, out var at)) return false;

        double? temperature = null;
        if (element.TryGetProperty("t", out var temperatureElement) && temperatureElement.ValueKind == JsonValueKind.Number)
        {
            temperature = temperatureElement.GetDouble();
        }

        point = new WeatherPoint(at, description.Trim(), temperature);
        return true;
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryDate(JsonElement element, out DateTimeOffset at)
    {
        at = default;

        foreach (var name in new[] { "local_datetime", "utc_datetime", "datetime" })
        {
            if (!TryString(element, name, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;

            if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out at))
            {
                return true;
            }
        }

        return false;
    }
}
