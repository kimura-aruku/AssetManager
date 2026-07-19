using AssetManager.Application.Settings;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.Infrastructure.Operations;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly DataRootLayout _layout;
    private readonly SettingsRepository _repository;

    public JsonAppSettingsStore(DataRootLayout layout, AtomicJsonFileStore? store = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _repository = new SettingsRepository(store ?? new AtomicJsonFileStore());
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var document = await _repository.LoadAsync(_layout, cancellationToken).ConfigureAwait(false);
        return new AppSettings(document.CheckPathsOnStartup, document.LicenseWarningDays);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _repository.SaveAsync(
            _layout,
            new AppSettingsDocument(
                JsonDefaults.CurrentSchemaVersion,
                settings.CheckPathsOnStartup,
                settings.LicenseWarningDays),
            cancellationToken);
    }
}
