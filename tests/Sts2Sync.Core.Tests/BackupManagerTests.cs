using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class BackupManagerTests
{
    private readonly InMemoryLocalSaveStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
    private BackupManager CreateManager(int max = 50) => new(_store, _clock, max);

    [Fact]
    public async Task Backup_CreatesFileWithCorrectName()
    {
        var manager = CreateManager();
        var data = "save data"u8.ToArray();

        await manager.BackupAsync("profile1/saves/progress.save", data, "cloud");

        var expectedTimestamp = _clock.GetUtcNow().ToUnixTimeSeconds();
        var expectedPath = $"profile1/backups/progress.save.{expectedTimestamp}.cloud.bak";
        var result = await _store.ReadFileAsync(expectedPath);
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task Backup_DifferentSources_CorrectlyLabeled()
    {
        var manager = CreateManager();

        await manager.BackupAsync("profile1/saves/progress.save", "d1"u8.ToArray(), "cloud");
        _clock.Advance(TimeSpan.FromSeconds(1));
        await manager.BackupAsync("profile1/saves/progress.save", "d2"u8.ToArray(), "local");

        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Equal(2, backups.Count);
        Assert.Contains(backups, b => b.Source == "cloud");
        Assert.Contains(backups, b => b.Source == "local");
    }

    [Fact]
    public async Task Backup_PreservesOriginalContent()
    {
        var manager = CreateManager();
        var data = "important save data with special chars: {}"u8.ToArray();

        await manager.BackupAsync("profile1/saves/current_run.save", data, "local");

        var backups = await manager.ListBackupsAsync("profile1");
        var backup = Assert.Single(backups);
        var content = await _store.ReadFileAsync(backup.BackupPath);
        Assert.Equal(data, content);
    }

    [Fact]
    public async Task ListBackups_ReturnsAllForProfile()
    {
        var manager = CreateManager();

        await manager.BackupAsync("profile1/saves/progress.save", "d1"u8.ToArray(), "cloud");
        _clock.Advance(TimeSpan.FromSeconds(1));
        await manager.BackupAsync("profile1/saves/current_run.save", "d2"u8.ToArray(), "local");

        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Equal(2, backups.Count);
    }

    [Fact]
    public async Task ListBackups_Empty_ReturnsEmpty()
    {
        var manager = CreateManager();
        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Empty(backups);
    }

    [Fact]
    public async Task ListBackups_DoesNotIncludeOtherProfiles()
    {
        var manager = CreateManager();

        await manager.BackupAsync("profile1/saves/progress.save", "d1"u8.ToArray(), "cloud");
        await manager.BackupAsync("profile2/saves/progress.save", "d2"u8.ToArray(), "cloud");

        var p1Backups = await manager.ListBackupsAsync("profile1");
        var p2Backups = await manager.ListBackupsAsync("profile2");

        Assert.Single(p1Backups);
        Assert.Single(p2Backups);
    }

    [Fact]
    public async Task ListBackups_OriginalPathReconstructed()
    {
        var manager = CreateManager();
        await manager.BackupAsync("profile1/saves/progress.save", "d"u8.ToArray(), "cloud");

        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Equal("profile1/saves/progress.save", backups[0].OriginalPath);
    }

    [Fact]
    public async Task ListBackups_OrderedByTimestampDescending()
    {
        var manager = CreateManager();

        await manager.BackupAsync("profile1/saves/progress.save", "d1"u8.ToArray(), "cloud");
        _clock.Advance(TimeSpan.FromSeconds(10));
        await manager.BackupAsync("profile1/saves/progress.save", "d2"u8.ToArray(), "local");
        _clock.Advance(TimeSpan.FromSeconds(10));
        await manager.BackupAsync("profile1/saves/progress.save", "d3"u8.ToArray(), "cloud");

        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Equal(3, backups.Count);
        Assert.True(backups[0].UnixTimestamp > backups[1].UnixTimestamp);
        Assert.True(backups[1].UnixTimestamp > backups[2].UnixTimestamp);
    }

    [Fact]
    public async Task Prune_UnderLimit_DeletesNothing()
    {
        var manager = CreateManager(max: 50);

        for (int i = 0; i < 10; i++)
        {
            await manager.BackupAsync("profile1/saves/progress.save", "d"u8.ToArray(), "cloud");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var deleted = await manager.PruneAsync("profile1");

        Assert.Equal(0, deleted);
        var backups = await manager.ListBackupsAsync("profile1");
        Assert.Equal(10, backups.Count);
    }

    [Fact]
    public async Task Prune_AtLimit_DeletesNothing()
    {
        var manager = CreateManager(max: 5);

        for (int i = 0; i < 5; i++)
        {
            await manager.BackupAsync("profile1/saves/progress.save", "d"u8.ToArray(), "cloud");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var deleted = await manager.PruneAsync("profile1");

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task Prune_OverLimit_DeletesOldest()
    {
        var manager = CreateManager(max: 3);

        for (int i = 0; i < 5; i++)
        {
            await manager.BackupAsync("profile1/saves/progress.save", [(byte)i], "cloud");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var deleted = await manager.PruneAsync("profile1");

        Assert.Equal(2, deleted);
        var remaining = await manager.ListBackupsAsync("profile1");
        Assert.Equal(3, remaining.Count);
        // Newest 3 kept — they have timestamps 2,3,4 seconds after start
        Assert.All(remaining, b => Assert.True(b.UnixTimestamp >= remaining[^1].UnixTimestamp));
    }

    [Fact]
    public async Task Prune_PreservesNewestBackups()
    {
        var manager = CreateManager(max: 2);

        // Create 4 backups with known content
        for (int i = 0; i < 4; i++)
        {
            await manager.BackupAsync("profile1/saves/progress.save", [(byte)(i + 10)], "cloud");
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        await manager.PruneAsync("profile1");

        var remaining = await manager.ListBackupsAsync("profile1");
        Assert.Equal(2, remaining.Count);

        // The two newest should be the ones with bytes 13 and 12
        var content0 = await _store.ReadFileAsync(remaining[0].BackupPath);
        var content1 = await _store.ReadFileAsync(remaining[1].BackupPath);
        Assert.Equal(13, content0[0]);
        Assert.Equal(12, content1[0]);
    }
}

/// <summary>
/// Minimal fake TimeProvider for deterministic timestamp testing.
/// </summary>
internal class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}
