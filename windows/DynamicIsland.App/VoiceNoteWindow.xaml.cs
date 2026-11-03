using System.Windows;
using DynamicIsland.Core;
using Windows.Media.SpeechRecognition;

namespace DynamicIsland.App;

/// <summary>
/// Catatan suara cepat (Tahap 5b).
///
/// Mendengarkan satu ucapan dengan mesin pengenalan di perangkat, lalu mengisi
/// kotak teks yang tetap bisa diedit. Bila pengenalan suara tidak tersedia atau
/// izin mikrofon ditolak, alasannya ditampilkan dan catatan masih bisa diketik.
/// </summary>
public sealed partial class VoiceNoteWindow : Window, IDisposable
{
    private readonly IslandText text;
    private SpeechRecognizer? recognizer;
    private bool listening;
    private bool closed;

    public VoiceNoteWindow(IslandText text)
    {
        InitializeComponent();

        this.text = text;

        TitleText.Text = text.Get("voice.title");
        RetryButton.Content = text.Get("voice.retry");
        SaveButton.Content = text.Get("voice.save");
        CancelButton.Content = text.Get("voice.cancel");
        StatusText.Text = text.Get("voice.hint");

        Loaded += async (_, _) => await ListenAsync();
        Closed += (_, _) => Dispose();
    }

    public void Dispose()
    {
        DisposeRecognizer();
        GC.SuppressFinalize(this);
    }

    public event Action<VoiceNoteResult>? Accepted;

    private async Task ListenAsync()
    {
        if (listening || closed) return;

        listening = true;
        StatusText.Text = text.Get("voice.listening");

        try
        {
            DisposeRecognizer();

            recognizer = new SpeechRecognizer();
            var compile = await recognizer.CompileConstraintsAsync();

            if (compile.Status != SpeechRecognitionResultStatus.Success)
            {
                ShowFailure(compile.Status.ToString());
                return;
            }

            var result = await recognizer.RecognizeAsync();

            if (result.Status == SpeechRecognitionResultStatus.Success)
            {
                Input.Text = result.Text;
                StatusText.Text = text.Get("voice.hint");
                return;
            }

            ShowFailure(result.Status.ToString());
        }
        catch (Exception error) when (error is UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            ShowFailure(error.Message);
        }
        finally
        {
            listening = false;
        }
    }

    private void ShowFailure(string reason)
    {
        Log.Write("suara: pengenalan gagal (" + reason + ")");
        StatusText.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            text.Get("voice.failed"),
            reason);
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        await ListenAsync();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var transcript = Input.Text.Trim();
        if (transcript.Length == 0) return;

        Accepted?.Invoke(VoiceNotes.Parse(transcript, DateTimeOffset.Now));
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void DisposeRecognizer()
    {
        closed = true;
        recognizer?.Dispose();
        recognizer = null;
    }
}
