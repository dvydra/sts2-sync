using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface IBackupManager
{
    Task BackupAsync(string relativePath, byte[] data, string source, CancellationToken ct = default);
    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(string profile, CancellationToken ct = default);
    Task<int> PruneAsync(string profile, CancellationToken ct = default);
}
