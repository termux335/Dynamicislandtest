using System.Security.Cryptography;
using System.Windows.Threading;
using DynamicIsland.Core;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland.App;

/// <summary>
/// Now Playing sungguhan lewat SystemMediaTransportControls: membaca judul,
/// artis, posisi, dan status putar dari sesi yang sedang aktif, lalu mengirim
/// perintah putar/jeda/berikutnya. Tanpa izin khusus, hanya sesi media lokal.
///
/// Semua panggilan WinRT berjalan di thread latar; hasilnya dikirim ke UI
/// lewat dispatcher supaya window tidak pernah menunggu.
/// </summary>
public sealed class MediaMonitor : IDisposable
{
    public const string ActivityId = "media";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);

    private readonly IslandController controller;
    private readonly Dispatcher dispatcher;
    private readonly IslandText text;
    private readonly CancellationTokenSource stop = new();

    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private bool shown;

    /// <summary>Lagu yang liriknya sudah dimuat, supaya berkas tidak dibaca tiap detik.</summary>
    private readonly RepeatTrackDetector repeatDetector = new();
    private string? lyricsKey;

    private IReadOnlyList<LyricLine> lyrics = Array.Empty<LyricLine>();

    private string? artworkKey;

    /// <summary>Byte sampul album (PNG/JPEG) saat lagu berganti; array kosong saat tidak ada.</summary>
    public event Action<byte[]>? ArtworkChanged;

    public MediaMonitor(IslandController controller, Dispatcher dispatcher, IslandText? text = null)
    {
        this.controller = controller;
        this.dispatcher = dispatcher;
        this.text = text ?? IslandText.Default;
    }

    public void Start() => _ = Task.Run(RunAsync);

    public void TogglePlayPause() => Command(session => session.TryTogglePlayPauseAsync());

    public void Next() => Command(session => session.TrySkipNextAsync());

    public void Previous() => Command(session => session.TrySkipPreviousAsync());

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }

    private async Task RunAsync()
    {
        Log.Write("media: meminta sesi Now Playing");

        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync()
                .AsTask()
                .WaitAsync(StartupTimeout, stop.Token);

            Log.Write("media: Now Playing tersedia");
        }
        catch (Exception error)
        {
            Log.Write("media: tidak tersedia (" + error.Message + ")");
            return;
        }

        using var ticker = new PeriodicTimer(PollInterval);

        try
        {
            while (await ticker.WaitForNextTickAsync(stop.Token))
            {
                await PollAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // App ditutup.
        }
    }

    private void Command(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var session = manager?.GetCurrentSession();
                if (session is null)
                {
                    Log.Write("media: tidak ada sesi aktif");
                    return;
                }

                var accepted = await command(session);
                Log.Write(accepted ? "media: perintah diterima" : "media: perintah ditolak pemutar");
                await PollAsync();
            }
            catch (Exception error)
            {
                Log.Write("media: perintah gagal (" + error.Message + ")");
            }
        });
    }

    private async Task PollAsync()
    {
        try
        {
            var session = manager?.GetCurrentSession();
            if (session is null)
            {
                Publish(null);
                return;
            }

            var properties = await session.TryGetMediaPropertiesAsync();

            await PublishArtworkAsync(properties);

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            var playing = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var position = timeline.Position;
            if (playing) position += DateTimeOffset.Now - timeline.LastUpdatedTime;

            var end = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : timeline.MaxSeekTime;
            var progress = end > TimeSpan.Zero
                ? Math.Clamp(position.TotalSeconds / end.TotalSeconds, 0, 1)
                : (double?)null;

            var artist = properties?.Artist?.Trim();
            var subtitle = string.Join(
                " · ",
                new[] { artist, properties?.AlbumTitle }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

            var title = string.IsNullOrWhiteSpace(properties?.Title) ? "Media" : properties!.Title;

            // Lirik hanya dibaca ulang saat lagunya berganti.
            var key = title + "\u0001" + artist;
            if (!string.Equals(key, lyricsKey, StringComparison.Ordinal))
            {
                lyricsKey = key;
                Log.Write($"media: lagu {title}" + (string.IsNullOrEmpty(artist) ? string.Empty : $" — {artist}"));
                lyrics = LyricsSource.Load(artist, title);

                if (repeatDetector.TrackPlayed(title, artist) && text.Humor == HumorMode.Kocak)
                {
                    _ = dispatcher.BeginInvoke(() =>
                        controller.Refresh(new Activity(
                            id: "humor-repeat",
                            kind: ActivityKind.Notification,
                            title: text.Get("humor.repeat"),
                            isLive: false,
                            expiresAt: DateTimeOffset.Now.AddSeconds(4))));
                }
            }

            var caption = Lyrics.At(lyrics, position);

            Publish(new Activity(
                id: ActivityId,
                kind: ActivityKind.Media,
                title: title,
                subtitle: subtitle.Length > 0 ? subtitle : null,
                progress: progress,
                actions: new[]
                {
                    new ActivityAction("media-toggle", text.Get(playing ? "media.pause" : "media.play"), playing ? "pause" : "play"),
                    new ActivityAction("media-previous", text.Get("media.previous"), "previous"),
                    new ActivityAction("media-next", text.Get("media.next"), "next"),
                },
                position: position,
                duration: end > TimeSpan.Zero ? end : null,
                caption: caption));
        }
        catch (Exception error)
        {
            Log.Write("media: baca sesi gagal (" + error.Message + ")");
        }
    }

    /// <summary>
    /// Membaca sampul sekali per lagu dan meneruskannya ke UI. Warna dipilih di
    /// Core; monitor ini hanya memindahkan byte.
    /// </summary>
    private async Task PublishArtworkAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? properties)
    {
        byte[]? bytes = null;

        if (properties?.Thumbnail is { } reference)
        {
            try
            {
                using var stream = await reference.OpenReadAsync();
                if (stream.Size > 0)
                {
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    await reader.LoadAsync((uint)stream.Size);

                    bytes = new byte[stream.Size];
                    reader.ReadBytes(bytes);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
            {
                Log.Write("media: sampul tidak terbaca (" + error.Message + ")");
            }
        }

        var key = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
        if (string.Equals(key, artworkKey, StringComparison.Ordinal)) return;

        artworkKey = key;
        ArtworkChanged?.Invoke(bytes ?? Array.Empty<byte>());
    }

    private void Publish(Activity? activity)
    {
        if (dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(() =>
        {
            if (activity is null)
            {
                if (!shown) return;
                shown = false;
                controller.Remove(ActivityId);
                return;
            }

            shown = true;
            controller.Refresh(activity);
        });
    }
}
