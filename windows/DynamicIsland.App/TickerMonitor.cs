using System.Net.Http;
using System.Windows.Threading;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Ticker opsional (Tahap 5c.16). Hanya aktif bila pengguna menyalakannya dan
/// memberi URL http(s); kegagalan jaringan ditampilkan, bukan didiamkan.
/// </summary>
public sealed class TickerMonitor : IDisposable
{
    public const string ActivityId = "ticker";

    private readonly IslandController controller;
    private readonly DispatcherTimer timer;
    private readonly IslandText text;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };

    private IReadOnlyList<TickerItem> items = Array.Empty<TickerItem>();
    private string? configuredUrl;
    private DateTimeOffset? lastFetch;
    private int index;
    private string? error;
    private bool fetching;

    public TickerMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        this.text = text ?? IslandText.Default;

        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(8),
            DispatcherPriority.Background,
            (_, _) => Tick(),
            dispatcher);
    }

    public bool Enabled { get; private set; }

    public string? Url { get; private set; }

    public void Configure(bool enabled, string? url)
    {
        var next = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        var changed = !string.Equals(next, configuredUrl, StringComparison.Ordinal);

        configuredUrl = next;
        Url = next;
        Enabled = enabled && TickerFeed.ValidUrl(Url);

        if (!Enabled)
        {
            items = Array.Empty<TickerItem>();
            lastFetch = null;
            error = null;
            controller.Remove(ActivityId);
            return;
        }

        // URL baru tidak boleh menunggu jadwal lama 15 menit.
        if (changed)
        {
            items = Array.Empty<TickerItem>();
            lastFetch = null;
            index = 0;
            error = null;
        }

        if (lastFetch is null) _ = FetchAsync();
    }

    public void Start() => timer.Start();

    public void Dispose()
    {
        timer.Stop();
        client.Dispose();
        controller.Remove(ActivityId);
    }

    private void Tick()
    {
        if (!Enabled)
        {
            if (items.Count > 0 || error is not null) controller.Remove(ActivityId);
            items = Array.Empty<TickerItem>();
            error = null;
            return;
        }

        if (TickerFeed.ShouldRefresh(lastFetch, DateTimeOffset.Now, Enabled))
        {
            _ = FetchAsync();
            return;
        }

        if (error is not null)
        {
            ShowError();
            return;
        }

        if (items.Count == 0) return;

        index = (index + 1) % items.Count;
        Show(items[index]);
    }

    private async Task FetchAsync()
    {
        if (fetching || !Enabled || Url is not { } url) return;

        fetching = true;

        try
        {
            var xml = await client.GetStringAsync(url);

            items = TickerFeed.Parse(xml);
            lastFetch = DateTimeOffset.Now;
            error = items.Count == 0 ? text.Get("ticker.empty") : null;
            index = 0;

            if (error is not null) ShowError();
            else Show(items[0]);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            error = exception.Message;
            lastFetch = DateTimeOffset.Now;
            Log.Write("ticker: gagal memuat (" + exception.Message + ")");
            ShowError();
        }
        finally
        {
            fetching = false;
        }
    }

    private void Show(TickerItem item) => controller.Refresh(new Activity(
        id: ActivityId,
        kind: ActivityKind.Device,
        priority: 12,
        title: text.Get("ticker.title"),
        subtitle: item.Title,
        isLive: true,
        state: ActivityState.Compact));

    private void ShowError() => controller.Refresh(new Activity(
        id: ActivityId,
        kind: ActivityKind.Notification,
        priority: 74,
        title: text.Get("ticker.title"),
        subtitle: text.Format("ticker.failed", error ?? text.Get("ticker.empty")),
        isLive: false,
        expiresAt: DateTimeOffset.Now.AddSeconds(10)));
}
