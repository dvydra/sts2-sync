using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class CloudFileCacheTests
{
    private static readonly CloudFileInfo[] SampleFiles =
    [
        new("profile1/saves/progress.save", 1024, 0, 1700000000, "abc123", null),
        new("profile1/saves/current_run.save", 2048, 0, 1700000100, "def456", null),
        new("profile1/saves/current_run_mp.save", 512, 0, 1700000050, "ghi789", null),
        new("profile1/history/1700000000.run", 4096, 0, 1700000000, "hist01", null),
        new("profile1/history/1700001000.run", 4096, 0, 1700001000, "hist02", null),
        new("profile1/prefs.save", 256, 0, 1700000000, "pref01", null),
        new("profile2/saves/progress.save", 1024, 0, 1699999000, "xyz999", null),
        new("profile2/saves/current_run.save", 0, 0, 1699999000, "empty1", null),
    ];

    [Fact]
    public void PrePopulated_IsLoaded()
    {
        var cache = new CloudFileCache(SampleFiles);
        Assert.True(cache.IsLoaded);
        Assert.Equal(8, cache.FileCount);
    }

    [Fact]
    public void GetFile_ExistingFile_ReturnsInfo()
    {
        var cache = new CloudFileCache(SampleFiles);
        var file = cache.GetFile("profile1/saves/progress.save");

        Assert.NotNull(file);
        Assert.Equal(1024u, file!.FileSize);
        Assert.Equal("abc123", file.FileSha);
        Assert.Equal(1700000000u, file.Timestamp);
    }

    [Fact]
    public void GetFile_NonExistent_ReturnsNull()
    {
        var cache = new CloudFileCache(SampleFiles);
        Assert.Null(cache.GetFile("profile3/saves/progress.save"));
    }

    [Fact]
    public void GetFilesInDirectory_ReturnsSorted()
    {
        var cache = new CloudFileCache(SampleFiles);
        var saves = cache.GetFilesInDirectory("profile1/saves");

        Assert.Equal(3, saves.Count);
        // Ordinal sort: _ < . so current_run_mp comes before current_run
        Assert.Equal("profile1/saves/current_run_mp.save", saves[0].Filename);
        Assert.Equal("profile1/saves/current_run.save", saves[1].Filename);
        Assert.Equal("profile1/saves/progress.save", saves[2].Filename);
    }

    [Fact]
    public void GetFilesInDirectory_WithTrailingSlash_Works()
    {
        var cache = new CloudFileCache(SampleFiles);
        var saves = cache.GetFilesInDirectory("profile1/saves/");
        Assert.Equal(3, saves.Count);
    }

    [Fact]
    public void GetFilesInDirectory_History_ReturnsRunFiles()
    {
        var cache = new CloudFileCache(SampleFiles);
        var history = cache.GetFilesInDirectory("profile1/history");
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void GetFilesInDirectory_NonExistentPrefix_ReturnsEmpty()
    {
        var cache = new CloudFileCache(SampleFiles);
        var files = cache.GetFilesInDirectory("profile3/saves");
        Assert.Empty(files);
    }

    [Fact]
    public void GetDirectories_ListsSubdirectories()
    {
        var cache = new CloudFileCache(SampleFiles);
        var dirs = cache.GetDirectories("profile1");

        Assert.Equal(2, dirs.Count);
        Assert.Contains("profile1/history/", dirs);
        Assert.Contains("profile1/saves/", dirs);
        // prefs.save is a file directly under profile1/, not a subdirectory
    }

    [Fact]
    public void GetDirectories_RootLevel_ListsProfiles()
    {
        var cache = new CloudFileCache(SampleFiles);
        // Files are like "profile1/saves/..." so root dirs are "profile1/", "profile2/"
        var dirs = cache.GetDirectories("");

        Assert.Equal(2, dirs.Count);
        Assert.Contains("profile1/", dirs);
        Assert.Contains("profile2/", dirs);
    }

    [Fact]
    public void HasCloudFiles_WhenLoaded_ReturnsTrue()
    {
        var cache = new CloudFileCache(SampleFiles);
        Assert.True(cache.HasCloudFiles);
    }

    [Fact]
    public void HasCloudFiles_WhenEmpty_ReturnsFalse()
    {
        var cache = new CloudFileCache(Array.Empty<CloudFileInfo>());
        Assert.False(cache.HasCloudFiles);
    }

    [Fact]
    public void AllFiles_ReturnsAllEntries()
    {
        var cache = new CloudFileCache(SampleFiles);
        Assert.Equal(8, cache.AllFiles.Count);
    }
}
