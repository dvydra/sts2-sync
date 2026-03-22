using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface ILocalSaveStore
{
    Task<List<string>> DetectProfilesAsync(CancellationToken ct = default);
    Task<List<LocalFileInfo>> GetFilesAsync(string profile, CancellationToken ct = default);
    Task<byte[]> ReadFileAsync(string relativePath, CancellationToken ct = default);
    Task WriteFileAsync(string relativePath, byte[] data, CancellationToken ct = default);
    Task<LocalFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default);
    Task<List<string>> ListFilesAsync(string directoryPrefix, string extension, CancellationToken ct = default);
    Task DeleteFileAsync(string relativePath, CancellationToken ct = default);
}
