using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class InMemorySettingsStore : ISettingsStore
{
    private SavePathSettings? _savePath;

    public Task<SavePathSettings?> LoadSavePathAsync()
        => Task.FromResult(_savePath);

    public Task SaveSavePathAsync(SavePathSettings settings)
    {
        _savePath = settings;
        return Task.CompletedTask;
    }
}
