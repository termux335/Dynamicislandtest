using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIsland.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace DynamicIsland.App;

/// <summary>
/// Riwayat clipboard aman (Tahap 5a).
///
/// Membaca clipboard lewat listener Windows biasa (tanpa hook keyboard), menyamarkan
/// kata sandi/token/nomor kartu sebelum menyimpan, dan hanya menyimpan di memori.
/// Gambar dicoba dibaca dengan OCR bawaan Windows; bila tidak tersedia, alasannya
/// dicatat dan gambar tetap disimpan sebagai entri tanpa teks.
/// </summary>
internal sealed class ClipboardMonitor : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private const int HwndMessage = -3;

    private readonly ClipboardHistory history;
    private readonly Dispatcher dispatcher;
    private readonly IslandText text;
    private HwndSource? source;
    private bool listening;
    private DateTimeOffset lastOcrFailure = DateTimeOffset.MinValue;

    public ClipboardMonitor(ClipboardHistory history, Dispatcher dispatcher, IslandText text)
    {
        this.history = history;
        this.dispatcher = dispatcher;
        this.text = text;
    }

    public ClipboardHistory History => history;

    public bool OcrEnabled { get; set; } = true;

    public event Action<ClipboardItem>? Captured;

    public void SetEnabled(bool enabled)
    {
        if (enabled == listening) return;

        if (enabled) Start();
        else Stop();
    }

    public void Dispose() => Stop();

    private void Start()
    {
        try
        {
            if (source is null)
            {
                var parameters = new HwndSourceParameters("DynamicIslandClipboard")
                {
                    WindowStyle = 0,
                    Width = 0,
                    Height = 0,
                    ParentWindow = new IntPtr(HwndMessage),
                };

                source = new HwndSource(parameters);
                source.AddHook(WndProc);
            }

            AddClipboardFormatListener(source.Handle);
            listening = true;
            Log.Write("clipboard: riwayat aktif (memori saja, data sensitif disamarkan)");
        }
        catch (Exception error) when (error is ExternalException or InvalidOperationException)
        {
            Log.Write("clipboard: listener gagal dimulai (" + error.Message + ")");
        }
    }

    private void Stop()
    {
        if (!listening) return;

        listening = false;

        if (source is not null)
        {
            RemoveClipboardFormatListener(source.Handle);
            source.RemoveHook(WndProc);
            source.Dispose();
            source = null;
        }

        Log.Write("clipboard: riwayat dimatikan");
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmClipboardUpdate) return IntPtr.Zero;

        _ = dispatcher.BeginInvoke(Capture);
        return IntPtr.Zero;
    }

    private void Capture()
    {
        if (!listening) return;

        try
        {
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                var value = Clipboard.GetText(TextDataFormat.UnicodeText);
                var kind = ClipboardSafety.IsLink(value) ? ClipboardKind.Link : ClipboardKind.Text;
                Add(kind, value);
                return;
            }

            if (Clipboard.ContainsImage())
            {
                _ = CaptureImageAsync();
            }
        }
        catch (ExternalException error)
        {
            // Clipboard sedang dipakai proses lain; coba lagi pada salinan berikutnya.
            Log.Write("clipboard: gagal membaca (" + error.Message + ")");
        }
    }

    private async Task CaptureImageAsync()
    {
        var recognized = OcrEnabled ? await TryOcrAsync() : null;
        var value = string.IsNullOrWhiteSpace(recognized) ? text.Get("clipboard.image") : recognized.Trim();

        Add(ClipboardKind.Image, value);
    }

    private async Task<string?> TryOcrAsync()
    {
        BitmapSource? image;

        try
        {
            image = Clipboard.GetImage();
        }
        catch (ExternalException)
        {
            return null;
        }

        if (image is null) return null;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));

            using var buffer = new MemoryStream();
            encoder.Save(buffer);

            using var stream = new InMemoryRandomAccessStream();

            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(buffer.ToArray());
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);

            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine is null)
            {
                LogOcrFailure("tidak ada bahasa OCR terpasang");
                return null;
            }

            var result = await engine.RecognizeAsync(bitmap);
            return result.Text;
        }
        catch (Exception error) when (error is ExternalException or IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            LogOcrFailure(error.Message);
            return null;
        }
    }

    private void LogOcrFailure(string reason)
    {
        // Jangan spam log: satu pesan per 10 menit.
        if (DateTimeOffset.Now - lastOcrFailure < TimeSpan.FromMinutes(10)) return;

        lastOcrFailure = DateTimeOffset.Now;
        Log.Write("clipboard: OCR tidak tersedia (" + reason + "); gambar tetap disimpan");
    }

    private void Add(ClipboardKind kind, string? value)
    {
        if (history.Add(kind, value) is not { } item) return;

        Log.Write(item.Redacted
            ? $"clipboard: {kind} disamarkan"
            : $"clipboard: {kind} disimpan di memori");

        Captured?.Invoke(item);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr window);
}
