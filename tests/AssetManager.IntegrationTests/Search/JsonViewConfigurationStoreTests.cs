using AssetManager.Application.Search;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.Search;

public sealed class JsonViewConfigurationStoreTests
{
    [Fact]
    public async Task SavedSearchesCanBeSavedLoadedAndDeleted()
    {
        using var temporary = new TemporaryDirectory();
        var layout = await InitializeAsync(temporary);
        var store = new JsonViewConfigurationStore(layout);
        var service = new SearchConfigurationService(store);
        var query = new SearchQuery(new Dictionary<FieldId, FieldSearchCondition>
        {
            [BuiltInFieldIds.Name] = new TextContainsCondition("forest texture"),
            [BuiltInFieldIds.Favorite] = new BooleanEqualsCondition(true),
            [BuiltInFieldIds.Tags] = new OptionAnyCondition(["tag.nature", "tag.fantasy"]),
        });

        var saved = await service.SaveSearchAsync("自然素材", query);
        var loaded = Assert.Single(await service.LoadSavedSearchesAsync());

        Assert.Equal(saved.Id, loaded.Id);
        Assert.Equal("自然素材", loaded.Name);
        Assert.IsType<TextContainsCondition>(loaded.Query.Conditions[BuiltInFieldIds.Name]);
        Assert.True(Assert.IsType<BooleanEqualsCondition>(
            loaded.Query.Conditions[BuiltInFieldIds.Favorite]).Value);
        Assert.Equal(
            ["tag.nature", "tag.fantasy"],
            Assert.IsType<OptionAnyCondition>(loaded.Query.Conditions[BuiltInFieldIds.Tags]).OptionIds);

        await service.DeleteSearchAsync(saved.Id);
        Assert.Empty(await service.LoadSavedSearchesAsync());
    }

    [Fact]
    public async Task ViewColumnSettingsRoundTripWithoutRemovingSavedSearches()
    {
        using var temporary = new TemporaryDirectory();
        var layout = await InitializeAsync(temporary);
        var service = new SearchConfigurationService(new JsonViewConfigurationStore(layout));
        _ = await service.SaveSearchAsync(
            "検索",
            new SearchQuery(new Dictionary<FieldId, FieldSearchCondition>
            {
                [BuiltInFieldIds.Name] = new TextContainsCondition("素材"),
            }));
        var columns = new ViewColumnSetting[]
        {
            new("builtin.targetPath", 1, 420, true),
            new("builtin.name", 0, 280, true),
            new("custom.hidden", 2, 180, false),
        };

        await service.SaveViewColumnsAsync(columns);
        var loaded = await service.LoadViewColumnsAsync();

        Assert.Equal(["builtin.name", "builtin.targetPath", "custom.hidden"], loaded.Select(item => item.Id));
        Assert.Equal(420, loaded[1].Width);
        Assert.False(loaded[2].Visible);
        Assert.Single(await service.LoadSavedSearchesAsync());
    }

    private static async Task<DataRootLayout> InitializeAsync(TemporaryDirectory temporary)
    {
        var result = await new DataSetInitializer(new AppDataPaths(temporary.Path)).InitializeAsync();
        return new DataRootLayout(result.DataRoot);
    }
}
