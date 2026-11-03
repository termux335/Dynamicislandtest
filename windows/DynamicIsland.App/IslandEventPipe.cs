using System.IO;
using System.IO.Pipes;
using System.Text;

namespace DynamicIsland.App;

/// <summary>
/// Menerima event JSON dari proses lain lewat named pipe lokal.
/// Hanya lokal (bukan jaringan), tanpa izin tambahan.
/// </summary>
public sealed class IslandEventPipe : IDisposable
{
    public const string DefaultPipeName = "DynamicIslandEvents";

    private readonly Action<string> onLine;
    private readonly CancellationTokenSource cancellation = new();

    public IslandEventPipe(Action<string> onLine, string? name = null)
    {
        this.onLine = onLine;
        PipeName = string.IsNullOrWhiteSpace(name) ? DefaultPipeName : name;
    }

    public string PipeName { get; }

    public void Start() => _ = Task.Run(RunAsync);

    private async Task RunAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await using (server)
                {
                    await server.WaitForConnectionAsync(cancellation.Token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    while (true)
                    {
                        var line = await reader.ReadLineAsync(cancellation.Token);
                        if (line is null) break;

                        var trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

                        onLine(trimmed);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Klien putus di tengah baris; tunggu koneksi berikutnya.
            }
            catch (UnauthorizedAccessException)
            {
                // Nama pipe sedang dipakai; coba lagi.
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
