namespace Sts2Sync.Core.Services;

public class ConsoleSyncLogger : ISyncLogger
{
    public void Info(string message) => Console.WriteLine($"[Sync] {message}");
    public void Warn(string message) => Console.WriteLine($"[Sync WARN] {message}");
    public void Error(string message, Exception? ex = null) =>
        Console.WriteLine($"[Sync ERROR] {message}{(ex is not null ? $": {ex.Message}" : "")}");
}
