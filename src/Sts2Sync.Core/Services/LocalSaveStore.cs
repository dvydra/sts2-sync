using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class LocalSaveStore : ILocalSaveStore
{
    private readonly string _basePath;

    public LocalSaveStore(string basePath)
    {
        _basePath = basePath;
    }

    public Task<List<string>> DetectProfilesAsync(CancellationToken ct = default)
    {
        var profiles = Constants.ProfileNames
            .Where(p => Directory.Exists(Path.Combine(_basePath, p)))
            .ToList();

        return Task.FromResult(profiles);
    }

    public Task<List<LocalFileInfo>> GetFilesAsync(string profile, CancellationToken ct = default)
    {
        var files = new List<LocalFileInfo>();

        foreach (var saveFile in Constants.SaveFileNames)
        {
            var relativePath = $"{profile}/{saveFile}";
            var fullPath = Path.Combine(_basePath, relativePath);

            if (File.Exists(fullPath))
            {
                files.Add(BuildFileInfo(relativePath, fullPath));
            }
        }

        return Task.FromResult(files);
    }

    public async Task<byte[]> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}", fullPath);

        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public async Task WriteFileAsync(string relativePath, byte[] data, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(fullPath, data, ct);
    }

    public Task<LocalFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);

        if (!File.Exists(fullPath))
            return Task.FromResult<LocalFileInfo?>(null);

        return Task.FromResult<LocalFileInfo?>(BuildFileInfo(relativePath, fullPath));
    }

    private string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        var normalizedBase = Path.GetFullPath(_basePath + Path.DirectorySeparatorChar);

        if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
            throw new ArgumentException($"Path escapes base directory: {relativePath}");

        return fullPath;
    }

    private static LocalFileInfo BuildFileInfo(string relativePath, string fullPath)
    {
        var data = File.ReadAllBytes(fullPath);
        var lastWrite = File.GetLastWriteTimeUtc(fullPath);

        return new LocalFileInfo(
            relativePath,
            data.Length,
            SaveFileHasher.ComputeSha256(data),
            SaveFileHasher.ComputeSha1(data),
            new DateTimeOffset(lastWrite, TimeSpan.Zero));
    }
}
