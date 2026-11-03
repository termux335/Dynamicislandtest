using System.Windows.Threading;
using DynamicIsland.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland.App;

/// <summary>
/// Membaca notifikasi toast Windows lewat <see cref="UserNotificationListener"/>.
///
/// Izin diminta hanya saat fitur dinyalakan dari menu klik kanan, dan app tidak
/// pernah membaca isi notifikasi tanpa izin itu. Bila Windows menolak (atau
/// belum mendukung untuk app tanpa paket MSIX), alasannya ditampilkan sebagai
/// alert singkat — bukan gagal diam-diam.
///
/// Isi notifikasi diproses di mesin ini saja: tidak disimpan ke cakram dan
/// tidak dikirim ke mana pun.
/// </summary>
public sealed class NotificationMonitor : IDisposable
{
    public const string ActivityId = "windows-notification";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly IslandController controller;
    private readonly Dispatcher dispatcher;
    private readonly IslandText text;
    private readonly CancellationTokenSource stop = new();

    private UserNotificationListener? listener;
    private uint? latestId;
    private uint? shownId;
    private bool started;

    /// <summary>
    /// Isi notifikasi disembunyikan saat layar dibagikan atau direkam. Judul dan
    /// isi tetap di memori; yang berubah hanya teks yang tampil.
    /// </summary>
    public bool PrivacyHidden
    {
        get => privacyHidden;
        set
        {
            if (privacyHidden == value) return;

            privacyHidden = value;
            shownId = null;
            Log.Write(value ? "notifikasi: isi disembunyikan (berbagi layar/presentasi)" : "notifikasi: isi ditampilkan lagi");
        }
    }

    /// <summary>Menahan notifikasi non-kritis selama mode belajar.</summary>
    public bool Suppress { get; set; }

    private bool privacyHidden;

    public NotificationMonitor(IslandController controller, Dispatcher dispatcher, IslandText text)
    {
        this.controller = controller;
        this.dispatcher = dispatcher;
        this.text = text;
    }

    /// <summary>Menyalakan atau mematikan pemantauan; saat menyala izin diminta.</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (started) return;
            started = true;
            Log.Write("notifikasi: meminta izin Windows");
            _ = Task.Run(RunAsync);
            return;
        }

        if (!started) return;

        started = false;
        listener = null;
        latestId = null;
        shownId = null;
        Log.Write("notifikasi: dimatikan dari menu");
        Remove();
    }

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }

    /// <summary>Menandai notifikasi yang sedang tampil sebagai sudah dibaca.</summary>
    public void DismissLatest()
    {
        if (listener is null || latestId is not { } id)
        {
            Log.Write("notifikasi: tidak ada notifikasi untuk ditandai");
            return;
        }

        try
        {
            listener.RemoveNotification(id);
            Log.Write("notifikasi: ditandai dibaca");
            latestId = null;
            shownId = null;
            Remove();
        }
        catch (Exception error)
        {
            Log.Write("notifikasi: gagal menandai dibaca (" + error.Message + ")");
        }
    }

    private async Task RunAsync()
    {
        try
        {
            listener = UserNotificationListener.Current;
            var access = await listener.RequestAccessAsync().AsTask();

            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Log.Write("notifikasi: izin " + access);
                listener = null;
                started = false;
                ReportBlocked(access);
                return;
            }

            Log.Write("notifikasi: izin diberikan");

            using var ticker = new PeriodicTimer(PollInterval);

            while (await ticker.WaitForNextTickAsync(stop.Token))
            {
                await PollAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // App ditutup.
        }
        catch (Exception error)
        {
            Log.Write("notifikasi: tidak tersedia (" + error.Message + ")");
            listener = null;
            started = false;
            ReportBlocked(UserNotificationListenerAccessStatus.Unspecified);
        }
    }

    private async Task PollAsync()
    {
        if (listener is not { } current) return;

        try
        {
            var notifications = await current.GetNotificationsAsync(NotificationKinds.Toast);
            var latest = notifications.OrderByDescending(item => item.CreationTime).FirstOrDefault();

            if (latest is null)
            {
                shownId = null;
                latestId = null;
                Publish(null);
                return;
            }

            // Notifikasi yang sama tidak ditampilkan ulang tiap 3 detik.
            if (shownId == latest.Id) return;

            // Mode belajar: notifikasi baru ditandai sudah dibaca supaya tidak
            // menumpuk dan muncul sekaligus saat mode berakhir.
            if (Suppress)
            {
                shownId = latest.Id;
                latestId = latest.Id;
                Log.Write("notifikasi: ditahan selama mode belajar");
                return;
            }

            var app = latest.AppInfo?.DisplayInfo?.DisplayName ?? string.Empty;
            var lines = latest.Notification?.Visual
                ?.GetBinding(KnownNotificationBindings.ToastGeneric)
                ?.GetTextElements()
                ?.Select(element => element.Text)
                ?.Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList() ?? new List<string>();

            var title = lines.Count > 0 ? lines[0] : string.IsNullOrEmpty(app) ? text.Get("notifications.blockedTitle") : app;
            var subtitle = lines.Count > 1
                ? string.Join(" ", lines.Skip(1))
                : string.IsNullOrEmpty(app) ? null : app;

            if (privacyHidden)
            {
                title = text.Get("notification.hidden");
                subtitle = null;
            }

            latestId = latest.Id;
            shownId = latest.Id;
            // Hanya pengirim yang dicatat: isi notifikasi tetap di memori.
            Log.Write(string.IsNullOrEmpty(app) ? "notifikasi: pesan baru" : $"notifikasi: pesan dari {app}");
            Publish(new Activity(
                id: ActivityId,
                kind: ActivityKind.Notification,
                title: title,
                subtitle: subtitle,
                isLive: false,
                expiresAt: DateTimeOffset.Now.AddMilliseconds(IslandEvent.DefaultTtlMs),
                actions: new[] { new ActivityAction("notification.dismiss", text.Get("notifications.dismiss"), "check") }));
        }
        catch (Exception error)
        {
            Log.Write("notifikasi: gagal membaca (" + error.Message + ")");
        }
    }

    /// <summary>Alasan izin tidak diberikan, supaya pengguna tahu harus apa.</summary>
    private void ReportBlocked(UserNotificationListenerAccessStatus status)
    {
        var detail = status == UserNotificationListenerAccessStatus.Denied
            ? "Denied"
            : "Unspecified";

        var subtitle = status == UserNotificationListenerAccessStatus.Denied
            ? text.Format("notifications.denied", detail)
            : text.Get("notifications.unavailable");

        if (dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(() => controller.Refresh(new Activity(
            id: "notifications-blocked",
            kind: ActivityKind.Notification,
            title: text.Get("notifications.blockedTitle"),
            subtitle: subtitle,
            isLive: false,
            expiresAt: DateTimeOffset.Now.AddMilliseconds(IslandEvent.DefaultTtlMs * 2))));
    }

    private void Publish(Activity? activity)
    {
        if (dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(() =>
        {
            if (activity is null)
            {
                Remove();
                return;
            }

            controller.Refresh(activity);
        });
    }

    private void Remove()
    {
        if (dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(() => controller.Remove(ActivityId));
    }
}
