using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class ProfileScannerTests
{
    private readonly InMemoryLocalSaveStore _localStore = new();

    private static CloudFileCache EmptyCloudCache()
        => new(Array.Empty<CloudFileInfo>());

    private static CloudFileCache CloudCacheWith(params CloudFileInfo[] files)
        => new(files);

    // Helper: build cloud file info with a SHA-1 matching the given content
    private static CloudFileInfo CloudFile(string filename, byte[] content)
        => new(filename, (uint)content.Length, 0, 1700000000,
            SaveFileHasher.ComputeSha1(content), null);

    private static CloudFileInfo CloudFile(string filename, string sha, uint size = 1024)
        => new(filename, size, 0, 1700000000, sha, null);

    [Fact]
    public async Task Scan_NoLocalNoCloud_AllProfilesPresent()
    {
        var scanner = new ProfileScanner(_localStore, EmptyCloudCache());

        var results = await scanner.ScanAsync();

        Assert.Equal(3, results.Count);
        Assert.All(results, p =>
        {
            Assert.False(p.ExistsLocally);
            Assert.False(p.ExistsInCloud);
            Assert.All(p.Files, f =>
            {
                Assert.Null(f.Local);
                Assert.Null(f.Cloud);
                Assert.False(f.ContentMatch);
            });
        });
    }

    [Fact]
    public async Task Scan_LocalOnly_ShowsLocalFiles()
    {
        var data = "{ \"test\": true }"u8.ToArray();
        _localStore.AddFile("profile1/saves/progress.save", data);

        var scanner = new ProfileScanner(_localStore, EmptyCloudCache());
        var results = await scanner.ScanAsync();

        var p1 = results.Single(r => r.ProfileName == "profile1");
        Assert.True(p1.ExistsLocally);
        Assert.False(p1.ExistsInCloud);

        var progress = p1.Files.Single(f => f.SaveFileName == "saves/progress.save");
        Assert.NotNull(progress.Local);
        Assert.Null(progress.Cloud);
        Assert.False(progress.ContentMatch);
    }

    [Fact]
    public async Task Scan_CloudOnly_ShowsCloudFiles()
    {
        var cache = CloudCacheWith(
            CloudFile("profile2/saves/progress.save", "someSha1", 1024));

        var scanner = new ProfileScanner(_localStore, cache);
        var results = await scanner.ScanAsync();

        var p2 = results.Single(r => r.ProfileName == "profile2");
        Assert.False(p2.ExistsLocally);
        Assert.True(p2.ExistsInCloud);

        var progress = p2.Files.Single(f => f.SaveFileName == "saves/progress.save");
        Assert.Null(progress.Local);
        Assert.NotNull(progress.Cloud);
        Assert.False(progress.ContentMatch);
    }

    [Fact]
    public async Task Scan_BothSides_MatchingContent_ContentMatchTrue()
    {
        var data = "{ \"matching\": true }"u8.ToArray();
        _localStore.AddFile("profile1/saves/progress.save", data);

        var cache = CloudCacheWith(CloudFile("profile1/saves/progress.save", data));

        var scanner = new ProfileScanner(_localStore, cache);
        var results = await scanner.ScanAsync();

        var p1 = results.Single(r => r.ProfileName == "profile1");
        Assert.True(p1.ExistsLocally);
        Assert.True(p1.ExistsInCloud);

        var progress = p1.Files.Single(f => f.SaveFileName == "saves/progress.save");
        Assert.NotNull(progress.Local);
        Assert.NotNull(progress.Cloud);
        Assert.True(progress.ContentMatch);
    }

    [Fact]
    public async Task Scan_BothSides_DifferentContent_ContentMatchFalse()
    {
        _localStore.AddFile("profile1/saves/progress.save", "local data"u8.ToArray());

        var cache = CloudCacheWith(
            CloudFile("profile1/saves/progress.save", "different_sha1"));

        var scanner = new ProfileScanner(_localStore, cache);
        var results = await scanner.ScanAsync();

        var progress = results
            .Single(r => r.ProfileName == "profile1")
            .Files.Single(f => f.SaveFileName == "saves/progress.save");

        Assert.NotNull(progress.Local);
        Assert.NotNull(progress.Cloud);
        Assert.False(progress.ContentMatch);
    }

    [Fact]
    public async Task Scan_MixedProfiles_SomeLocalSomeCloud()
    {
        _localStore.AddFile("profile1/saves/progress.save", "data"u8.ToArray());

        var cache = CloudCacheWith(
            CloudFile("profile2/saves/progress.save", "cloudsha", 512));

        var scanner = new ProfileScanner(_localStore, cache);
        var results = await scanner.ScanAsync();

        var p1 = results.Single(r => r.ProfileName == "profile1");
        Assert.True(p1.ExistsLocally);
        Assert.False(p1.ExistsInCloud);

        var p2 = results.Single(r => r.ProfileName == "profile2");
        Assert.False(p2.ExistsLocally);
        Assert.True(p2.ExistsInCloud);

        var p3 = results.Single(r => r.ProfileName == "profile3");
        Assert.False(p3.ExistsLocally);
        Assert.False(p3.ExistsInCloud);
    }

    [Fact]
    public async Task Scan_MultipleFilesPerProfile()
    {
        var progressData = "progress"u8.ToArray();
        var runData = "current_run"u8.ToArray();
        _localStore.AddFile("profile1/saves/progress.save", progressData);
        _localStore.AddFile("profile1/saves/current_run.save", runData);

        var cache = CloudCacheWith(
            CloudFile("profile1/saves/progress.save", progressData),
            CloudFile("profile1/saves/current_run.save", "different_sha"));

        var scanner = new ProfileScanner(_localStore, cache);
        var results = await scanner.ScanAsync();

        var p1 = results.Single(r => r.ProfileName == "profile1");
        Assert.Equal(3, p1.Files.Count); // 3 save file slots

        var progress = p1.Files.Single(f => f.SaveFileName == "saves/progress.save");
        Assert.True(progress.ContentMatch);

        var run = p1.Files.Single(f => f.SaveFileName == "saves/current_run.save");
        Assert.False(run.ContentMatch);

        var mp = p1.Files.Single(f => f.SaveFileName == "saves/current_run_mp.save");
        Assert.Null(mp.Local);
        Assert.Null(mp.Cloud);
    }
}
