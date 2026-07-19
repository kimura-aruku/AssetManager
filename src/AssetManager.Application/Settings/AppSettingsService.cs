using AssetManager.Domain.Licensing;

namespace AssetManager.Application.Settings;

public sealed record AppSettings(bool CheckPathsOnStartup, int LicenseWarningDays)
{
    public AppSettings Validate()
    {
        _ = new LicenseWarningPolicy(LicenseWarningDays);
        return this;
    }
}

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsService(IAppSettingsStore store)
{
    private readonly IAppSettingsStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _store.LoadAsync(cancellationToken);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _store.SaveAsync(settings.Validate(), cancellationToken);
    }
}
