using System.IO.Compression;
using System.Security.Cryptography;
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
        Log($"EnumerateFilesAsync: starting for appId={appId}");
        var allFiles = new List<CloudFileInfo>();
        uint startIndex = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            Log($"EnumerateFilesAsync: requesting page at index {startIndex}...");
            var request = new CCloud_EnumerateUserFiles_Request
            {
                appid = appId,
                start_index = startIndex,
                count = PageSize
            };

            var response = await SendCloudRequestAsync<
                CCloud_EnumerateUserFiles_Request,
                CCloud_EnumerateUserFiles_Response>(
                "EnumerateUserFiles", request, ct);
            Log($"EnumerateFilesAsync: got {response.files.Count} files");

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

    public async Task UploadFileAsync(uint appId, string filename, byte[] data, CancellationToken ct = default)
    {
        var fileSha = SHA1.HashData(data);
        var rawSize = (uint)data.Length;

        // Try compression — use only if smaller
        var compressed = TryCompress(data, filename);
        var uploadBytes = compressed ?? data;
        var uploadSize = (uint)uploadBytes.Length;

        // Step 1: BeginAppUploadBatch
        var batchRequest = new CCloud_BeginAppUploadBatch_Request
        {
            appid = appId,
            machine_name = "android"
        };
        batchRequest.files_to_upload.Add(filename);

        var batchResponse = await SendCloudRequestAsync<
            CCloud_BeginAppUploadBatch_Request,
            CCloud_BeginAppUploadBatch_Response>(
            "BeginAppUploadBatch", batchRequest, ct);

        var batchId = batchResponse.batch_id;

        // Step 2: ClientBeginFileUpload
        var beginRequest = new CCloud_ClientBeginFileUpload_Request
        {
            appid = appId,
            filename = filename,
            file_size = uploadSize,
            raw_file_size = rawSize,
            file_sha = fileSha,
            time_stamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            upload_batch_id = batchId,
            can_encrypt = false,
            is_shared_file = false
        };

        var beginResponse = await SendCloudRequestAsync<
            CCloud_ClientBeginFileUpload_Request,
            CCloud_ClientBeginFileUpload_Response>(
            "ClientBeginFileUpload", beginRequest, ct);

        // Step 3: Upload blocks via HTTP
        var uploadSucceeded = false;
        try
        {
            foreach (var block in beginResponse.block_requests)
            {
                ct.ThrowIfCancellationRequested();

                var url = $"https://{block.url_host}{block.url_path}";
                var method = block.http_method == 2 ? HttpMethod.Post : HttpMethod.Put;

                using var httpRequest = new HttpRequestMessage(method, url);

                var bodyData = block.explicit_body_data is { Length: > 0 }
                    ? block.explicit_body_data
                    : uploadBytes[(int)block.block_offset..((int)block.block_offset + (int)block.block_length)];

                httpRequest.Content = new ByteArrayContent(bodyData);
                httpRequest.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                httpRequest.Content.Headers.ContentLength = bodyData.Length;

                foreach (var header in block.request_headers)
                    httpRequest.Headers.TryAddWithoutValidation(header.name, header.value);

                var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
                httpResponse.EnsureSuccessStatusCode();
            }

            uploadSucceeded = true;
        }
        finally
        {
            // Step 4: ClientCommitFileUpload (always called, even on failure)
            await SendCloudRequestAsync<
                CCloud_ClientCommitFileUpload_Request,
                CCloud_ClientCommitFileUpload_Response>(
                "ClientCommitFileUpload",
                new CCloud_ClientCommitFileUpload_Request
                {
                    transfer_succeeded = uploadSucceeded,
                    appid = appId,
                    file_sha = fileSha,
                    filename = filename
                }, ct);
        }

        // Step 5: CompleteAppUploadBatchBlocking
        await SendCloudRequestAsync<
            CCloud_CompleteAppUploadBatch_Request,
            CCloud_CompleteAppUploadBatch_Response>(
            "CompleteAppUploadBatchBlocking",
            new CCloud_CompleteAppUploadBatch_Request
            {
                appid = appId,
                batch_id = batchId,
                batch_eresult = (uint)EResult.OK
            }, ct);
    }

    private static readonly TimeSpan RpcTimeout = TimeSpan.FromSeconds(60);

    private async Task<TResponse> SendCloudRequestAsync<TRequest, TResponse>(
        string method, TRequest request, CancellationToken ct)
        where TRequest : class, ProtoBuf.IExtensible, new()
        where TResponse : class, ProtoBuf.IExtensible, new()
    {
        ct.ThrowIfCancellationRequested();

        Log($"RPC: Cloud.{method} — connected={_connection.IsConnected}, state={_connection.State}");
        Log($"RPC: Cloud.{method} — UnifiedMessages type={_connection.UnifiedMessages?.GetType().Name ?? "NULL"}");

        var job = _connection.UnifiedMessages
            .SendMessage<TRequest, TResponse>($"Cloud.{method}#1", request);
        job.Timeout = RpcTimeout;
        Log($"RPC: Cloud.{method} — job created, id={job.JobID}, timeout={job.Timeout.TotalSeconds}s");

        try
        {
            var response = await job.ToTask();
            Log($"RPC: Cloud.{method} — result={response.Result}");

            if (response.Result != EResult.OK)
                throw new SteamCloudException(method, response.Result);

            return response.Body;
        }
        catch (Exception ex)
        {
            Log($"RPC: Cloud.{method} — FAILED: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
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
    /// <summary>
    /// Attempt ZIP compression. Returns compressed bytes if smaller than original, null otherwise.
    /// </summary>
    public static byte[]? TryCompress(byte[] data, string entryName = "data")
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(data);
        }

        var compressed = ms.ToArray();
        return compressed.Length < data.Length ? compressed : null;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[SteamCloud] {message}");
        Console.Out.Flush();
    }
}

public class SteamCloudException(string method, EResult result)
    : Exception($"Cloud.{method} failed: {result}")
{
    public string Method { get; } = method;
    public EResult Result { get; } = result;
}
