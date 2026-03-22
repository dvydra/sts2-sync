using System.IO.Compression;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SteamCloudServiceTests
{
    [Fact]
    public void TryDecompress_NotCompressed_ReturnsNull()
    {
        var data = "plain text data"u8.ToArray();
        var result = SteamCloudService.TryDecompress(data, rawFileSize: 0, fileSize: 15);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecompress_SameSizes_ReturnsNull()
    {
        var data = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00 };
        var result = SteamCloudService.TryDecompress(data, rawFileSize: 5, fileSize: 5);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecompress_NoZipMagic_ReturnsNull()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        var result = SteamCloudService.TryDecompress(data, rawFileSize: 10, fileSize: 5);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecompress_TooShort_ReturnsNull()
    {
        var data = new byte[] { 0x50, 0x4B };
        var result = SteamCloudService.TryDecompress(data, rawFileSize: 10, fileSize: 2);
        Assert.Null(result);
    }

    [Fact]
    public void TryDecompress_ValidZip_Decompresses()
    {
        // Create a real ZIP in memory
        var originalContent = "Hello, STS2 save data!"u8.ToArray();
        byte[] zipData;

        using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("data.txt");
                using var entryStream = entry.Open();
                entryStream.Write(originalContent);
            }
            zipData = ms.ToArray();
        }

        var result = SteamCloudService.TryDecompress(
            zipData,
            rawFileSize: (uint)originalContent.Length,
            fileSize: (uint)zipData.Length);

        Assert.NotNull(result);
        Assert.Equal(originalContent, result);
    }

    [Fact]
    public void TryDecompress_EmptyZip_ReturnsNull()
    {
        // Create an empty ZIP
        byte[] zipData;
        using (var ms = new MemoryStream())
        {
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                // No entries
            }
            zipData = ms.ToArray();
        }

        var result = SteamCloudService.TryDecompress(
            zipData, rawFileSize: 100, fileSize: (uint)zipData.Length);

        // Empty ZIP has no magic PK\x03\x04 for individual entries,
        // but the end-of-central-directory starts with PK\x05\x06
        // so our magic check should catch this
        Assert.Null(result);
    }

    // --- TryCompress tests ---

    [Fact]
    public void TryCompress_SmallData_ReturnsNull()
    {
        // Very small data — ZIP overhead makes it larger
        var data = "tiny"u8.ToArray();
        var result = SteamCloudService.TryCompress(data);
        Assert.Null(result);
    }

    [Fact]
    public void TryCompress_LargeRepetitiveData_ReturnsCompressed()
    {
        // Highly compressible: 10KB of repeating JSON
        var data = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Repeat("{\"key\":\"value\",\"num\":12345},", 400)));

        var result = SteamCloudService.TryCompress(data);

        Assert.NotNull(result);
        Assert.True(result!.Length < data.Length);
    }

    [Fact]
    public void TryCompress_RoundTrip_WithDecompress()
    {
        var original = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Repeat("{\"floors_climbed\":100,\"playtime\":5000},", 200)));

        var compressed = SteamCloudService.TryCompress(original);
        Assert.NotNull(compressed);

        var decompressed = SteamCloudService.TryDecompress(
            compressed!, rawFileSize: (uint)original.Length, fileSize: (uint)compressed!.Length);

        Assert.NotNull(decompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void SteamCloudException_ContainsMethodAndResult()
    {
        var ex = new SteamCloudException("EnumerateUserFiles", SteamKit2.EResult.Fail);
        Assert.Equal("EnumerateUserFiles", ex.Method);
        Assert.Equal(SteamKit2.EResult.Fail, ex.Result);
        Assert.Contains("EnumerateUserFiles", ex.Message);
        Assert.Contains("Fail", ex.Message);
    }
}
