using AssetManager.Application.Search;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.Quality;

public sealed class ScaleAcceptanceTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(250)]
    [InlineData(1000)]
    public async Task 生成データで起動検索100件追加読み込みを確認する(int recordCount)
    {
        using var temporary = new TemporaryDirectory();
        var appPaths = new AppDataPaths(temporary.Path);
        var initializer = new DataSetInitializer(appPaths);
        var first = await initializer.InitializeAsync();
        var layout = new DataRootLayout(first.DataRoot);
        var repository = new RecordRepository(new AtomicJsonFileStore());
        var nameDefinition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Name);
        var now = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < recordCount; index++)
        {
            var record = AssetRecord.Create(now.AddTicks(index))
                .SetValue(nameDefinition, new TextFieldValue($"素材 {index:D4}"), now.AddTicks(index));
            await repository.SaveAsync(layout, new PersistedAssetRecord(record));
        }

        var restarted = await initializer.InitializeAsync();
        var loaded = await repository.LoadAllAsync(layout, BuiltInFieldCatalog.All);
        var query = new SearchQuery(
        [
            new KeyValuePair<FieldId, FieldSearchCondition>(
                BuiltInFieldIds.Name,
                new TextContainsCondition("素材")),
        ]);
        var session = new RecordSearchSession(loaded.Records.Select(item => item.Record), query);
        var initialVisibleCount = session.VisibleRecords.Count;
        var visibleCount = initialVisibleCount;
        while (session.HasMore)
        {
            visibleCount += session.LoadMore().Count;
        }

        Assert.Equal(recordCount, restarted.RecordCount);
        Assert.Equal(recordCount, session.TotalCount);
        Assert.Equal(Math.Min(RecordSearchSession.PageSize, recordCount), initialVisibleCount);
        Assert.Equal(recordCount, visibleCount);
    }
}
