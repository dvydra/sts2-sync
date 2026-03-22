namespace Sts2Sync.Core.Services;

public interface ISyncLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
