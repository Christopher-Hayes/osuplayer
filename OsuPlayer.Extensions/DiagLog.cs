using System.Runtime.CompilerServices;

namespace OsuPlayer.Extensions;

/// <summary>
/// Synchronous file-based diagnostic logger. Writes flush immediately so data is preserved
/// even if the process freezes or deadlocks before the next opportunity to write.
/// </summary>
public static class DiagLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "logs", "diag.log");
    private static readonly object Lock = new();

    static DiagLog()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); }
        catch { /* best effort */ }
    }

    /// <summary>Appends a timestamped line to <c>logs/diag.log</c>.</summary>
    public static void Write(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        try
        {
            var entry = $"{DateTime.UtcNow:HH:mm:ss.fff} [{Path.GetFileNameWithoutExtension(file)}.{member}:{line}] {message}{Environment.NewLine}";
            lock (Lock)
            {
                File.AppendAllText(LogPath, entry);
            }
        }
        catch { /* never throw from a diagnostic helper */ }
    }

    /// <summary>Writes a separator line marking a new app session.</summary>
    public static void StartSession()
    {
        try
        {
            var entry = $"{Environment.NewLine}==== SESSION START {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===={Environment.NewLine}";
            lock (Lock)
            {
                File.AppendAllText(LogPath, entry);
            }
        }
        catch { }
    }
}
