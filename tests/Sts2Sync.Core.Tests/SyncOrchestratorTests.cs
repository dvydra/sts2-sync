using System.Text;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SyncOrchestratorTests
{
    private readonly InMemoryLocalSaveStore _localStore = new();
    private readonly InMemoryCredentialStore _credentialStore = new();
    private readonly FakeAuthService _authService = new();
    private readonly TestableCloudService _cloudService = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

    private SyncOrchestrator CreateOrchestrator()
    {
        var cloudCache = new CloudFileCache(_cloudService, Constants.Sts2AppId);
        var backupManager = new BackupManager(_localStore, _clock);
        return new SyncOrchestrator(
            _authService, _cloudService, cloudCache,
            _localStore, backupManager, _credentialStore);
    }

    private void SetupAuth()
    {
        _credentialStore.SaveAsync(new SteamCredentials("user", "token", null)).Wait();
        _authService.LoggedIn = true;
    }

    // Helper: create progress save JSON with specific floors
    private static byte[] ProgressBytes(int floors) =>
        Encoding.UTF8.GetBytes($$"""{"floors_climbed": {{floors}}, "total_playtime": 100}""");

    [Fact]
    public async Task Sync_NotLoggedIn_NoCredentials_ReturnsError()
    {
        var orch = CreateOrchestrator();

        var report = await orch.SyncAsync(SyncDirection.Both);

        Assert.False(report.Success);
        Assert.Equal("Not logged in", report.Error);
    }

    [Fact]
    public async Task Sync_AuthFails_ReturnsError()
    {
        await _credentialStore.SaveAsync(new SteamCredentials("user", "bad_token", null));
        _authService.FailLogin = true;

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Both);

        Assert.False(report.Success);
        Assert.Contains("Authentication failed", report.Error);
    }

    [Fact]
    public async Task Sync_Identical_NoAction()
    {
        SetupAuth();
        var data = ProgressBytes(50);

        _localStore.AddFile("profile1/saves/progress.save", data);
        _cloudService.AddCloudFile("profile1/saves/progress.save", data);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Both);

        Assert.True(report.Success);
        Assert.True(report.Identical > 0);
        Assert.Equal(0, report.Downloaded);
        Assert.Equal(0, report.Uploaded);
    }

    [Fact]
    public async Task Sync_Download_CloudWins_WritesToLocal()
    {
        SetupAuth();
        var localData = ProgressBytes(10);
        var cloudData = ProgressBytes(100);

        _localStore.AddFile("profile1/saves/progress.save", localData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Download);

        Assert.True(report.Success);
        Assert.Equal(1, report.Downloaded);

        // Verify local was overwritten with cloud data
        var written = await _localStore.ReadFileAsync("profile1/saves/progress.save");
        Assert.Equal(cloudData, written);
    }

    [Fact]
    public async Task Sync_Upload_LocalWins_Uploads()
    {
        SetupAuth();
        var localData = ProgressBytes(100);
        var cloudData = ProgressBytes(10);

        _localStore.AddFile("profile1/saves/progress.save", localData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Upload);

        Assert.True(report.Success);
        Assert.Equal(1, report.Uploaded);
        Assert.Single(_cloudService.Uploads);
    }

    [Fact]
    public async Task Sync_Download_BacksUpLocalBeforeOverwrite()
    {
        SetupAuth();
        var localData = ProgressBytes(10);
        var cloudData = ProgressBytes(100);

        _localStore.AddFile("profile1/saves/progress.save", localData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var backupManager = new BackupManager(_localStore, _clock);
        var orch = new SyncOrchestrator(
            _authService, _cloudService,
            new CloudFileCache(_cloudService, Constants.Sts2AppId),
            _localStore, backupManager, _credentialStore);

        await orch.SyncAsync(SyncDirection.Download);

        var backups = await backupManager.ListBackupsAsync("profile1");
        Assert.Single(backups);
        Assert.Equal("local", backups[0].Source);

        // Backup contains old local data
        var backupContent = await _localStore.ReadFileAsync(backups[0].BackupPath);
        Assert.Equal(localData, backupContent);
    }

    [Fact]
    public async Task Sync_DirectionDownload_SkipsUploads()
    {
        SetupAuth();
        var localData = ProgressBytes(100); // local is newer
        var cloudData = ProgressBytes(10);

        _localStore.AddFile("profile1/saves/progress.save", localData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Download);

        Assert.True(report.Success);
        Assert.Equal(0, report.Uploaded); // No upload in download mode
        Assert.Empty(_cloudService.Uploads);
    }

    [Fact]
    public async Task Sync_DirectionUpload_SkipsDownloads()
    {
        SetupAuth();
        var localData = ProgressBytes(10); // cloud is newer
        var cloudData = ProgressBytes(100);

        _localStore.AddFile("profile1/saves/progress.save", localData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Upload);

        Assert.True(report.Success);
        Assert.Equal(0, report.Downloaded); // No download in upload mode

        // Local still has old data
        var local = await _localStore.ReadFileAsync("profile1/saves/progress.save");
        Assert.Equal(localData, local);
    }

    [Fact]
    public async Task Sync_CloudOnly_Downloads()
    {
        SetupAuth();
        var cloudData = ProgressBytes(50);
        _cloudService.AddCloudFile("profile1/saves/progress.save", cloudData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Download);

        Assert.True(report.Success);
        Assert.Equal(1, report.Downloaded);

        var written = await _localStore.ReadFileAsync("profile1/saves/progress.save");
        Assert.Equal(cloudData, written);
    }

    [Fact]
    public async Task Sync_LocalOnly_Uploads()
    {
        SetupAuth();
        var localData = ProgressBytes(50);
        _localStore.AddFile("profile1/saves/progress.save", localData);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Upload);

        Assert.True(report.Success);
        Assert.Equal(1, report.Uploaded);
        Assert.Single(_cloudService.Uploads);
    }

    [Fact]
    public async Task Sync_Report_CountsCorrect()
    {
        SetupAuth();

        // Profile 1: identical progress, cloud wins run
        var sameData = ProgressBytes(50);
        _localStore.AddFile("profile1/saves/progress.save", sameData);
        _cloudService.AddCloudFile("profile1/saves/progress.save", sameData);

        var localRun = """{"map_point_history": [[1,2]], "players": [{"deck": [], "relics": []}], "acts": [], "run_time": 10}"""u8.ToArray();
        var cloudRun = """{"map_point_history": [[1,2,3,4,5]], "players": [{"deck": [], "relics": []}], "acts": [], "run_time": 10}"""u8.ToArray();
        _localStore.AddFile("profile1/saves/current_run.save", localRun);
        _cloudService.AddCloudFile("profile1/saves/current_run.save", cloudRun);

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Both);

        Assert.True(report.Success);
        Assert.True(report.Identical >= 1); // progress.save matched
        Assert.Equal(1, report.Downloaded); // current_run cloud wins
    }

    [Fact]
    public async Task Sync_RunHistory_Merged()
    {
        SetupAuth();

        // Cloud-only .run file
        _cloudService.AddCloudFile("profile1/history/1700000000.run", "cloud run"u8.ToArray());
        // Local-only .run file
        _localStore.AddFile("profile1/history/1700001000.run", "local run"u8.ToArray());

        var orch = CreateOrchestrator();
        var report = await orch.SyncAsync(SyncDirection.Both);

        Assert.True(report.Success);
        Assert.Equal(1, report.RunHistoryDownloaded);
        Assert.Equal(1, report.RunHistoryUploaded);

        // Verify cloud run was downloaded locally
        var local = await _localStore.ReadFileAsync("profile1/history/1700000000.run");
        Assert.Equal("cloud run"u8.ToArray(), local);
    }
}

/// <summary>
/// Testable cloud service that supports both downloads and uploads.
/// </summary>
internal class TestableCloudService : ISteamCloudService
{
    private readonly Dictionary<string, byte[]> _cloudFiles = new();
    public List<(string Filename, byte[] Data)> Uploads { get; } = [];

    public void AddCloudFile(string filename, byte[] data)
    {
        _cloudFiles[filename] = data;
    }

    public IEnumerable<CloudFileInfo> EnumerateFiles()
    {
        return _cloudFiles.Select(kv => new CloudFileInfo(
            kv.Key,
            (uint)kv.Value.Length,
            0,
            1700000000,
            SaveFileHasher.ComputeSha1(kv.Value),
            null));
    }

    public Task<List<CloudFileInfo>> EnumerateFilesAsync(uint appId, CancellationToken ct = default)
        => Task.FromResult(EnumerateFiles().ToList());

    public Task<CloudFileContent> DownloadFileAsync(uint appId, string filename, CancellationToken ct = default)
    {
        if (!_cloudFiles.TryGetValue(filename, out var data))
            throw new FileNotFoundException($"Cloud file not found: {filename}");

        return Task.FromResult(new CloudFileContent(
            filename, data, SaveFileHasher.ComputeSha1(data), 1700000000));
    }

    public Task UploadFileAsync(uint appId, string filename, byte[] data, CancellationToken ct = default)
    {
        Uploads.Add((filename, data));
        _cloudFiles[filename] = data;
        return Task.CompletedTask;
    }
}

internal class FakeAuthService : ISteamAuthService
{
    public bool LoggedIn { get; set; }
    public bool FailLogin { get; set; }

    public bool IsLoggedIn => LoggedIn;

    public Task<AuthResult> LoginAsync(string username, string password,
        Func<AuthCodeRequest, Task<string>> codeProvider, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task LoginWithRefreshTokenAsync(SteamCredentials credentials, CancellationToken ct = default)
    {
        if (FailLogin)
            throw new InvalidOperationException("Login failed");
        LoggedIn = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync() { LoggedIn = false; return Task.CompletedTask; }
}
