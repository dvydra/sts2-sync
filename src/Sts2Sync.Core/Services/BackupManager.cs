using System.Text.RegularExpressions;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public partial class BackupManager : IBackupManager
{
    private readonly ILocalSaveStore _store;
    private readonly TimeProvider _clock;
    private readonly int _maxPerProfile;

    public BackupManager(ILocalSaveStore store, TimeProvider? clock = null, int maxPerProfile = 50)
    {
        _store = store;
        _clock = clock ?? TimeProvider.System;
        _maxPerProfile = maxPerProfile;
    }

    public async Task BackupAsync(string relativePath, byte[] data, string source, CancellationToken ct = default)
    {
        var profile = relativePath.Split('/')[0];
        var filename = Path.GetFileName(relativePath);
        var timestamp = _clock.GetUtcNow().ToUnixTimeSeconds();

        var backupPath = $"{profile}/backups/{filename}.{timestamp}.{source}.bak";
        await _store.WriteFileAsync(backupPath, data, ct);
    }

    public async Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(string profile, CancellationToken ct = default)
    {
        var files = await _store.ListFilesAsync($"{profile}/backups", ".bak", ct);
        var backups = new List<BackupInfo>();

        foreach (var path in files)
        {
            var match = BackupPattern().Match(Path.GetFileName(path));
            if (match.Success)
            {
                backups.Add(new BackupInfo(
                    $"{profile}/saves/{match.Groups["name"].Value}",
                    path,
                    long.Parse(match.Groups["ts"].Value),
                    match.Groups["source"].Value));
            }
        }

        return backups.OrderByDescending(b => b.UnixTimestamp).ToList();
    }

    public async Task<int> PruneAsync(string profile, CancellationToken ct = default)
    {
        var backups = await ListBackupsAsync(profile, ct);

        if (backups.Count <= _maxPerProfile)
            return 0;

        var toDelete = backups.Skip(_maxPerProfile).ToList();
        foreach (var backup in toDelete)
        {
            await _store.DeleteFileAsync(backup.BackupPath, ct);
        }

        return toDelete.Count;
    }

    // Matches: progress.save.1711234567.cloud.bak
    [GeneratedRegex(@"^(?<name>.+\.save)\.(?<ts>\d+)\.(?<source>\w+)\.bak$")]
    private static partial Regex BackupPattern();
}
