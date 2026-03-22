using System.IO.Compression;
using SteamKit2;
using SteamKit2.Internal;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class SteamCloudService : ISteamCloudService
{
    private readonly ISteamConnectionManager _connection;
    private readonly HttpClient _httpClient;
    private const uint PageSize = 500;

    public SteamCloudService(ISteamConnectionManager connection, HttpClient? httpClient = null)
    {
        _connection = connection;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<List<CloudFileInfo>> EnumerateFilesAsync(uint appId, CancellationToken ct = default)
    {
        var allFiles = new List<CloudFileInfo>();
        uint startIndex = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var request = new CCloud_EnumerateUserFiles_Request
            {
                appid = appId,
                start_index = startIndex,
                count = PageSize,
                extended_details = true
            };

            var response = await SendCloudRequestAsync<
                CCloud_EnumerateUserFiles_Request,
                CCloud_EnumerateUserFiles_Response>(
                "EnumerateUserFiles", request, ct);

            foreach (var file in response.files)
            {
                allFiles.Add(new CloudFileInfo(
                    Filename: file.filename,
                    FileSize: file.file_size,
                    RawFileSize: file.compressed_file_size,
                    Timestamp: file.timestamp,
                    FileSha: file.file_sha,
                    Url: string.IsNullOrEmpty(file.url) ? null : file.url
                ));
            }

            if (response.files.Count < PageSize)
                break;

            startIndex += (uint)response.files.Count;
        }

        return allFiles;
    }

    public async Task<CloudFileContent> DownloadFileAsync(uint appId, string filename, CancellationToken ct = default)
    {
        var request = new CCloud_ClientFileDownload_Request
        {
            appid = appId,
            filename = filename
        };

        var response = await SendCloudRequestAsync<
            CCloud_ClientFileDownload_Request,
            CCloud_ClientFileDownload_Response>(
            "ClientFileDownload", request, ct);

        // Always force HTTPS regardless of response flag to prevent downgrade attacks
        var url = $"https://{response.url_host}{response.url_path}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in response.request_headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.name, header.value);
        }

        var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
        httpResponse.EnsureSuccessStatusCode();

        var data = await httpResponse.Content.ReadAsByteArrayAsync(ct);

        // Decompress if needed
        var rawData = TryDecompress(data, response.raw_file_size, response.file_size) ?? data;

        var sha = SaveFileHasher.ComputeSha1(rawData);

        return new CloudFileContent(
            Filename: filename,
            Data: rawData,
            Sha: sha,
            Timestamp: response.time_stamp
        );
    }

    private async Task<TResponse> SendCloudRequestAsync<TRequest, TResponse>(
        string method, TRequest request, CancellationToken ct)
        where TRequest : class, ProtoBuf.IExtensible, new()
        where TResponse : class, ProtoBuf.IExtensible, new()
    {
        ct.ThrowIfCancellationRequested();

        var job = _connection.UnifiedMessages
            .SendMessage<TRequest, TResponse>($"Cloud.{method}#1", request);

        var response = await job.ToTask();

        if (response.Result != EResult.OK)
            throw new SteamCloudException(method, response.Result);

        return response.Body;
    }

    /// <summary>
    /// Attempt to decompress ZIP-compressed Steam Cloud data.
    /// Returns null if data is not compressed.
    /// </summary>
    public static byte[]? TryDecompress(byte[] data, uint rawFileSize, uint fileSize)
    {
        // Not compressed if raw size matches or is zero
        if (rawFileSize == 0 || rawFileSize == fileSize)
            return null;

        // Check ZIP magic bytes: PK\x03\x04
        if (data.Length < 4 || data[0] != 0x50 || data[1] != 0x4B || data[2] != 0x03 || data[3] != 0x04)
            return null;

        using var zipStream = new MemoryStream(data);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

        if (zip.Entries.Count == 0)
            return null;

        using var entry = zip.Entries[0].Open();
        using var ms = new MemoryStream();
        entry.CopyTo(ms);
        return ms.ToArray();
    }
}

public class SteamCloudException(string method, EResult result)
    : Exception($"Cloud.{method} failed: {result}")
{
    public string Method { get; } = method;
    public EResult Result { get; } = result;
}
