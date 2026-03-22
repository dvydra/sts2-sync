using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface ISettingsStore
{
    Task<SavePathSettings?> LoadSavePathAsync();
    Task SaveSavePathAsync(SavePathSettings settings);
}
