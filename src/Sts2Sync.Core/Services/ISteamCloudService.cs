using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface ISteamCloudService
{
    Task<List<CloudFileInfo>> EnumerateFilesAsync(uint appId, CancellationToken ct = default);
    Task<CloudFileContent> DownloadFileAsync(uint appId, string filename, CancellationToken ct = default);
    Task UploadFileAsync(uint appId, string filename, byte[] data, CancellationToken ct = default);
}
