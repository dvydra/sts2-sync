using System.Collections.Concurrent;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class CloudFileCache
{
    private readonly ISteamCloudService _cloudService;
    private readonly uint _appId;
    private ConcurrentDictionary<string, CloudFileInfo> _files = new();
    private bool _loaded;

    public bool IsLoaded => _loaded;
    public int FileCount => _files.Count;

    public CloudFileCache(ISteamCloudService cloudService, uint appId)
    {
        _cloudService = cloudService;
        _appId = appId;
    }

    /// <summary>
    /// For testing: create a pre-populated cache without a cloud service.
    /// </summary>
    internal CloudFileCache(IEnumerable<CloudFileInfo> files)
    {
        _cloudService = null!;
        _appId = 0;
        _files = new ConcurrentDictionary<string, CloudFileInfo>(
            files.ToDictionary(f => f.Filename));
        _loaded = true;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;
        await RefreshAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var files = await _cloudService.EnumerateFilesAsync(_appId, ct);
        var newCache = new ConcurrentDictionary<string, CloudFileInfo>(
            files.ToDictionary(f => f.Filename));
        _files = newCache;
        _loaded = true;
    }

    public CloudFileInfo? GetFile(string filename)
        => _files.TryGetValue(filename, out var info) ? info : null;

    public List<CloudFileInfo> GetFilesInDirectory(string prefix)
    {
        if (!prefix.EndsWith('/'))
            prefix += '/';

        return _files.Values
            .Where(f => f.Filename.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(f => f.Filename)
            .ToList();
    }

    public List<string> GetDirectories(string prefix)
    {
        if (prefix.Length > 0 && !prefix.EndsWith('/'))
            prefix += '/';

        return _files.Keys
            .Where(k => prefix.Length == 0 || k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k =>
            {
                var rest = k[prefix.Length..];
                var slashIdx = rest.IndexOf('/');
                return slashIdx >= 0 ? prefix + rest[..(slashIdx + 1)] : null;
            })
            .Where(d => d is not null)
            .Distinct()
            .Order()
            .ToList()!;
    }

    public bool HasCloudFiles => _loaded && _files.Count > 0;

    public IReadOnlyCollection<CloudFileInfo> AllFiles => _files.Values.ToList();
}
