using System.IO;

namespace DynamicIsland.App;

/// <summary>
/// Log untuk uji manual. App ini WinExe, jadi stderr tidak selalu terlihat;
/// semua pesan ditulis ke <c>%TEMP%\dynamic-island.log</c>.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(Path.GetTempPath(), "dynamic-island.log");

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Log tidak boleh menjatuhkan app.
            }
        }
    }
}
