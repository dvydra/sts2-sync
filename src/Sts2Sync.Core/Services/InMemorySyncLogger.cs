namespace Sts2Sync.Core.Services;

public class InMemorySyncLogger : ISyncLogger
{
    public List<(string Level, string Message)> Entries { get; } = [];

    public void Info(string message) => Entries.Add(("INFO", message));
    public void Warn(string message) => Entries.Add(("WARN", message));
    public void Error(string message, Exception? ex = null) =>
        Entries.Add(("ERROR", ex is not null ? $"{message}: {ex.Message}" : message));
}
