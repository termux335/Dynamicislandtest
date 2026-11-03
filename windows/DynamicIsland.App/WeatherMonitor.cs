using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Peringatan cuaca BMKG (Tahap 4). Modul ini **mati secara bawaan** dan hanya
/// memakai internet setelah pengguna menyalakannya serta mengisi kode ADM4.
///
/// Kegagalan (jaringan mati, kode salah) tidak pernah diam: alasannya masuk log
/// dan ditampilkan sebagai alert singkat.
/// </summary>
internal sealed class WeatherMonitor : IDisposable
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IslandController controller;
    private readonly Dispatcher dispatcher;
    private readonly IslandText text;
    private readonly DispatcherTimer timer;
    private bool enabled;
    private string? adm4;
    private bool shown;
    private bool failing;

    public WeatherMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.dispatcher = dispatcher;
        this.text = text;
        timer = new DispatcherTimer(
            TimeSpan.FromMinutes(30),
            DispatcherPriority.Background,
            (_, _) => _ = PollAsync(),
            dispatcher);
    }

    public bool Enabled => enabled;

    public static string Endpoint(string adm4) =>
        "https://api.bmkg.go.id/personal/prakiraan-cuaca?adm4=" + Uri.EscapeDataString(adm4);

    public void Configure(bool value, string? adm4Code)
    {
        var changed = enabled != value || !string.Equals(adm4, adm4Code, StringComparison.Ordinal);
        enabled = value;
        adm4 = string.IsNullOrWhiteSpace(adm4Code) ? null : adm4Code.Trim();

        if (!changed) return;

        if (!enabled)
        {
            timer.Stop();
            Remove();
            Log.Write("cuaca: dimatikan");
            return;
        }

        Log.Write("cuaca: aktif; memakai internet hanya untuk BMKG");
        _ = PollAsync();
        timer.Start();
    }

    public void Dispose()
    {
        timer.Stop();
    }

    private async Task PollAsync()
    {
        if (!enabled) return;

        if (adm4 is null)
        {
            ReportProblem(text.Get("settings.weatherUnavailable"));
            return;
        }

        try
        {
            var json = await Client.GetStringAsync(new Uri(Endpoint(adm4))).ConfigureAwait(false);
            var points = BmkgWeather.Parse(json);
            var warning = BmkgWeather.Warning(points, DateTimeOffset.Now);

            _ = dispatcher.BeginInvoke(() =>
            {
                if (!enabled) return;

                if (warning is null)
                {
                    Log.Write($"cuaca: {points.Count} titik dibaca, tidak ada peringatan");
                    Remove();
                    failing = false;
                    return;
                }

                Log.Write($"cuaca: peringatan {warning.Description} pukul {warning.At:HH:mm}");
                controller.Refresh(BmkgWeather.ActivityFor(warning, DateTimeOffset.Now, text));
                shown = true;
                failing = false;
            });
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or IOException or UriFormatException)
        {
            _ = dispatcher.BeginInvoke(() => ReportProblem(text.Format("weather.warning.subtitle", error.Message, "—")));
        }
    }

    /// <summary>Menampilkan sebab kegagalan sekali, bukan setiap 30 menit.</summary>
    private void ReportProblem(string reason)
    {
        Log.Write("cuaca: " + reason);
        if (failing) return;

        failing = true;
        controller.Refresh(new Activity(
            id: "weather-problem",
            kind: ActivityKind.Notification,
            priority: 20,
            title: text.Get("weather.warning.title"),
            subtitle: reason,
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddSeconds(10)));
    }

    private void Remove()
    {
        if (!shown) return;

        shown = false;
        controller.Remove("weather");
    }
}
