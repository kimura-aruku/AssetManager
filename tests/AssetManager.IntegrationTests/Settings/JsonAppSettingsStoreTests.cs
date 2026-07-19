using AssetManager.Application.Settings;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.IntegrationTests.Settings;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task 警告日数と起動時パス確認をJSONへ保存して復元する()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var repository = new SettingsRepository(new AtomicJsonFileStore());
        await repository.SaveAsync(
            layout,
            new AppSettingsDocument(JsonDefaults.CurrentSchemaVersion, true, 365));
        var service = new AppSettingsService(new JsonAppSettingsStore(layout));

        await service.SaveAsync(new AppSettings(false, 180));
        var loaded = await service.LoadAsync();

        Assert.False(loaded.CheckPathsOnStartup);
        Assert.Equal(180, loaded.LicenseWarningDays);
        var document = await repository.LoadAsync(layout);
        Assert.Equal(180, document.LicenseWarningDays);
    }
}
