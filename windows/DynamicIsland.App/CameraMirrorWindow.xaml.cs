using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynamicIsland.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace DynamicIsland.App;

/// <summary>
/// Cermin kamera singkat (Tahap 5a).
///
/// Kamera hanya hidup selama jendela ini terbuka. Tidak ada gambar yang disimpan,
/// ditulis ke cakram, atau dikirim; frame langsung ditampilkan lalu dibuang.
/// Menutup jendela atau menyembunyikan island menghentikan kamera.
/// </summary>
public sealed partial class CameraMirrorWindow : Window, IDisposable
{
    private readonly string deniedFormat;
    private readonly string note;
    private MediaCapture? capture;
    private MediaFrameReader? reader;
    private WriteableBitmap? bitmap;
    private bool stopping;

    public CameraMirrorWindow(IslandText text, string title, string note, string deniedFormat, string closeLabel)
    {
        InitializeComponent();

        this.note = note;
        this.deniedFormat = deniedFormat;

        TitleText.Text = title;
        StatusText.Text = note;
        CloseButton.Content = closeLabel;

        Loaded += async (_, _) => await StartAsync();
        Closed += (_, _) => Dispose();
    }

    public void Dispose()
    {
        _ = StopAsync();
        GC.SuppressFinalize(this);
    }

    public bool IsRunning => reader is not null;

    private async Task StartAsync()
    {
        try
        {
            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            });

            var source = capture.FrameSources.Values
                .FirstOrDefault(candidate => candidate.Info.SourceKind == MediaFrameSourceKind.Color);

            if (source is null) throw new InvalidOperationException("kamera tidak ditemukan");

            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            reader.FrameArrived += OnFrameArrived;

            var status = await reader.StartAsync();
            if (status != MediaFrameReaderStartStatus.Success) throw new InvalidOperationException("status " + status);

            Log.Write("kamera: pratinjau aktif; tidak ada gambar yang disimpan");

            // Jendela bisa ditutup saat inisialisasi masih berjalan.
            if (!IsVisible) await StopAsync();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Log.Write("kamera: gagal dimulai (" + error.Message + ")");
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, deniedFormat, error.Message);
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80));
            await StopAsync();
        }
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        SoftwareBitmap? copy = null;

        using (var frame = sender.TryAcquireLatestFrame())
        {
            var source = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (source is not null) copy = SoftwareBitmap.Copy(source);
        }

        if (copy is null) return;

        _ = Dispatcher.BeginInvoke(() =>
        {
            using (copy)
            {
                try
                {
                    Render(copy);
                }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException)
                {
                    Log.Write("kamera: frame dilewati (" + error.Message + ")");
                }
            }
        });
    }

    private unsafe void Render(SoftwareBitmap source)
    {
        using var converted = SoftwareBitmap.Convert(source, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        if (width <= 0 || height <= 0) return;

        if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
        {
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        }

        using var buffer = converted.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        var description = buffer.GetPlaneDescription(0);

        ((IMemoryBufferByteAccess)reference).GetBuffer(out var data, out _);

        bitmap.Lock();
        try
        {
            var rowBytes = Math.Min(description.Stride, bitmap.BackBufferStride);

            for (var row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(
                    data + (row * description.Stride),
                    (byte*)bitmap.BackBuffer + (row * bitmap.BackBufferStride),
                    bitmap.BackBufferStride,
                    rowBytes);
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        Preview.Source = bitmap;
    }

    /// <summary>Mematikan kamera dan melepas perangkat. Aman dipanggil berulang.</summary>
    public async Task StopAsync()
    {
        if (stopping) return;
        stopping = true;

        try
        {
            if (reader is not null)
            {
                reader.FrameArrived -= OnFrameArrived;
                await reader.StopAsync();
                reader.Dispose();
                reader = null;
            }

            capture?.Dispose();
            capture = null;

            Log.Write("kamera: dimatikan");
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or UnauthorizedAccessException)
        {
            Log.Write("kamera: gagal berhenti dengan rapi (" + error.Message + ")");
        }
        finally
        {
            Preview.Source = null;
            bitmap = null;
            StatusText.Text = note;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _ = StopAsync();
        Close();
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
