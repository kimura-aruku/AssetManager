using AssetManager.Application.Startup;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.Startup;

public sealed class DataSetInitializerTests
{
    [Fact]
    public async Task FirstStartupCreatesFixedAreaAndInitialData()
    {
        using var temporary = new TemporaryDirectory();
        var paths = new AppDataPaths(temporary.Path);
        var progress = new CollectingProgress();
        var initializer = new DataSetInitializer(paths);

        var result = await initializer.InitializeAsync(progress);
        var layout = new DataRootLayout(result.DataRoot);

        Assert.True(result.CreatedInitialData);
        Assert.Equal(0, result.RecordCount);
        Assert.Equal(365, result.LicenseWarningDays);
        Assert.True(Directory.Exists(paths.FixedAppRoot));
        Assert.True(Directory.Exists(paths.LogsDirectory));
        Assert.True(File.Exists(paths.BootstrapFile));
        Assert.True(File.Exists(layout.ManifestFile));
        Assert.True(File.Exists(layout.FieldsFile));
        Assert.True(File.Exists(layout.AssetTypesFile));
        Assert.True(File.Exists(layout.TagsFile));
        Assert.True(File.Exists(layout.SettingsFile));
        Assert.True(File.Exists(layout.ViewsFile));
        Assert.True(Directory.Exists(layout.UndoDirectory));
        Assert.Equal(7, progress.Values.Count);
        Assert.Equal(7, progress.Values[^1].CompletedSteps);

        var types = await new AssetTypeRepository(new AtomicJsonFileStore()).LoadAsync(layout);
        var tags = await new TagRepository(new AtomicJsonFileStore()).LoadAsync(layout);
        Assert.Equal(8, types.Count);
        Assert.Empty(tags.Categories);
        Assert.Empty(tags.Tags);
    }

    [Fact]
    public async Task RestartKeepsExistingSettingsAndRemovesStaleUndoHistory()
    {
        using var temporary = new TemporaryDirectory();
        var paths = new AppDataPaths(temporary.Path);
        var initializer = new DataSetInitializer(paths);
        var firstResult = await initializer.InitializeAsync();
        var layout = new DataRootLayout(firstResult.DataRoot);
        var settingsRepository = new SettingsRepository(new AtomicJsonFileStore());
        var customized = new AppSettingsDocument(1, false, 42);
        await settingsRepository.SaveAsync(layout, customized);
        var staleHistory = Path.Combine(layout.UndoDirectory, "stale-command.json");
        await File.WriteAllTextAsync(staleHistory, "{}");

        var secondResult = await initializer.InitializeAsync();

        Assert.False(secondResult.CreatedInitialData);
        Assert.Equal(42, secondResult.LicenseWarningDays);
        Assert.Equal(customized, await settingsRepository.LoadAsync(layout));
        Assert.False(File.Exists(staleHistory));
        Assert.True(Directory.Exists(layout.UndoDirectory));
    }

    private sealed class CollectingProgress : IProgress<StartupProgress>
    {
        public List<StartupProgress> Values { get; } = [];

        public void Report(StartupProgress value)
        {
            Values.Add(value);
        }
    }
}
