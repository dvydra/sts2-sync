using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class LocalSaveStoreTests
{
    private readonly InMemoryLocalSaveStore _store = new();

    [Fact]
    public async Task DetectProfiles_NoFiles_ReturnsEmpty()
    {
        var profiles = await _store.DetectProfilesAsync();
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task DetectProfiles_WithFiles_ReturnsMatchingProfiles()
    {
        _store.AddFile("profile1/saves/progress.save", "data1"u8.ToArray());
        _store.AddFile("profile3/saves/progress.save", "data3"u8.ToArray());

        var profiles = await _store.DetectProfilesAsync();

        Assert.Equal(["profile1", "profile3"], profiles.Order().ToList());
    }

    [Fact]
    public async Task DetectProfiles_IgnoresUnknownDirectories()
    {
        _store.AddFile("profile1/saves/progress.save", "data"u8.ToArray());
        _store.AddFile("unknown/saves/progress.save", "data"u8.ToArray());

        var profiles = await _store.DetectProfilesAsync();

        Assert.Equal(["profile1"], profiles);
    }

    [Fact]
    public async Task GetFiles_ExistingProfile_ReturnsMetadata()
    {
        var data = "{ \"test\": true }"u8.ToArray();
        _store.AddFile("profile1/saves/progress.save", data);

        var files = await _store.GetFilesAsync("profile1");

        var file = Assert.Single(files);
        Assert.Equal("profile1/saves/progress.save", file.RelativePath);
        Assert.Equal(data.Length, file.FileSize);
        Assert.NotEmpty(file.Sha256);
    }

    [Fact]
    public async Task GetFiles_EmptyProfile_ReturnsEmpty()
    {
        var files = await _store.GetFilesAsync("profile1");
        Assert.Empty(files);
    }

    [Fact]
    public async Task GetFiles_OnlyReturnsSaveFiles()
    {
        _store.AddFile("profile1/saves/progress.save", "data"u8.ToArray());
        _store.AddFile("profile1/saves/current_run.save", "data"u8.ToArray());
        _store.AddFile("profile1/history/12345.run", "data"u8.ToArray());

        var files = await _store.GetFilesAsync("profile1");

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.Contains("saves/", f.RelativePath));
    }

    [Fact]
    public async Task ReadFile_Exists_ReturnsContent()
    {
        var data = "hello world"u8.ToArray();
        _store.AddFile("profile1/saves/progress.save", data);

        var result = await _store.ReadFileAsync("profile1/saves/progress.save");

        Assert.Equal(data, result);
    }

    [Fact]
    public async Task ReadFile_NotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _store.ReadFileAsync("profile1/saves/progress.save"));
    }

    [Fact]
    public async Task WriteFile_CreatesNewFile()
    {
        var data = "new save data"u8.ToArray();

        await _store.WriteFileAsync("profile1/saves/progress.save", data);

        var result = await _store.ReadFileAsync("profile1/saves/progress.save");
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task WriteFile_OverwritesExisting()
    {
        _store.AddFile("profile1/saves/progress.save", "old"u8.ToArray());
        var newData = "new"u8.ToArray();

        await _store.WriteFileAsync("profile1/saves/progress.save", newData);

        var result = await _store.ReadFileAsync("profile1/saves/progress.save");
        Assert.Equal(newData, result);
    }

    [Fact]
    public async Task GetFileInfo_Exists_ReturnsMetadata()
    {
        var data = "test data"u8.ToArray();
        _store.AddFile("profile1/saves/progress.save", data);

        var info = await _store.GetFileInfoAsync("profile1/saves/progress.save");

        Assert.NotNull(info);
        Assert.Equal("profile1/saves/progress.save", info.RelativePath);
        Assert.Equal(data.Length, info.FileSize);
        Assert.NotEmpty(info.Sha256);
    }

    [Fact]
    public async Task GetFileInfo_NotFound_ReturnsNull()
    {
        var info = await _store.GetFileInfoAsync("profile1/saves/progress.save");
        Assert.Null(info);
    }

    [Fact]
    public async Task Sha256_IsConsistent()
    {
        var data = "same content"u8.ToArray();
        _store.AddFile("profile1/saves/progress.save", data);

        var info1 = await _store.GetFileInfoAsync("profile1/saves/progress.save");
        var files = await _store.GetFilesAsync("profile1");

        Assert.Equal(info1!.Sha256, files[0].Sha256);
        Assert.Equal(SaveFileHasher.ComputeSha256(data), info1.Sha256);
    }
}
