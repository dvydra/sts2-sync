using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class InMemoryLocalSaveStore : ILocalSaveStore
{
    private readonly Dictionary<string, (byte[] Data, DateTimeOffset Modified)> _files = new();

    public void AddFile(string relativePath, byte[] data, DateTimeOffset? modified = null)
    {
        _files[relativePath] = (data, modified ?? DateTimeOffset.UtcNow);
    }

    public Task<List<string>> DetectProfilesAsync(CancellationToken ct = default)
    {
        var profiles = _files.Keys
            .Select(k => k.Split('/')[0])
            .Where(p => Constants.ProfileNames.Contains(p))
            .Distinct()
            .Order()
            .ToList();

        return Task.FromResult(profiles);
    }

    public Task<List<LocalFileInfo>> GetFilesAsync(string profile, CancellationToken ct = default)
    {
        var files = new List<LocalFileInfo>();

        foreach (var saveFile in Constants.SaveFileNames)
        {
            var path = $"{profile}/{saveFile}";
            if (_files.TryGetValue(path, out var entry))
            {
                files.Add(new LocalFileInfo(
                    path,
                    entry.Data.Length,
                    SaveFileHasher.ComputeSha256(entry.Data),
                    SaveFileHasher.ComputeSha1(entry.Data),
                    entry.Modified));
            }
        }

        return Task.FromResult(files);
    }

    public Task<byte[]> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(relativePath, out var entry))
            throw new FileNotFoundException($"File not found: {relativePath}");

        return Task.FromResult(entry.Data);
    }

    public Task WriteFileAsync(string relativePath, byte[] data, CancellationToken ct = default)
    {
        _files[relativePath] = (data, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<LocalFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(relativePath, out var entry))
            return Task.FromResult<LocalFileInfo?>(null);

        var info = new LocalFileInfo(
            relativePath,
            entry.Data.Length,
            SaveFileHasher.ComputeSha256(entry.Data),
            SaveFileHasher.ComputeSha1(entry.Data),
            entry.Modified);

        return Task.FromResult<LocalFileInfo?>(info);
    }
}
