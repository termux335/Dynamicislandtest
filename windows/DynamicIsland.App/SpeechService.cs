using DynamicIsland.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace DynamicIsland.App;

/// <summary>
/// Membacakan notifikasi dengan suara Windows (Tahap 5b, aksesibilitas).
///
/// Hanya teks notifikasi yang diucapkan, dan hanya bila pengguna menyalakannya.
/// Tidak ada rekaman suara dan tidak ada pengiriman ke jaringan.
/// </summary>
internal sealed class SpeechService : IDisposable
{
    private readonly MediaPlayer player = new();
    private SpeechSynthesizer? synthesizer;
    private bool reportedFailure;

    public bool Enabled { get; set; }

    public void Speak(string? text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text)) return;

        _ = SpeakAsync(text.Trim());
    }

    private async Task SpeakAsync(string text)
    {
        try
        {
            synthesizer ??= new SpeechSynthesizer();
            player.Source = MediaSource.CreateFromStream(
                await synthesizer.SynthesizeTextToStreamAsync(text),
                "audio/wav");
            player.Play();
        }
        catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            if (reportedFailure) return;

            reportedFailure = true;
            Log.Write("suara: pembacaan notifikasi tidak tersedia (" + error.Message + ")");
        }
    }

    public void Dispose()
    {
        player.Dispose();
        synthesizer?.Dispose();
    }
}
