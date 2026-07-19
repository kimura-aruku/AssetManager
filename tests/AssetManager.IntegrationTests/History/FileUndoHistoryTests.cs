using AssetManager.Application.Fields;
using AssetManager.Application.History;
using AssetManager.Application.Records;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.History;
using AssetManager.Infrastructure.Operations;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Startup;

namespace AssetManager.IntegrationTests.History;

public sealed class FileUndoHistoryTests
{
    [Fact]
    public async Task RecordHistoryIsSavedInSessionDirectoryAndAppliedTransactionally()
    {
        using var temporary = new TemporaryDirectory();
        var layout = await InitializeAsync(temporary);
        var store = new JsonAssetManagerDataStore(layout);
        var persistence = new FileUndoHistoryPersistence(layout);
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = new RecordApplicationService(store, history: history);

        var record = await records.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("履歴対象"),
        });
        var sessionDirectory = Path.Combine(layout.UndoDirectory, persistence.SessionId.ToString("D"));
        var entryDirectory = Path.Combine(sessionDirectory, "entry-00000000");

        Assert.True(File.Exists(Path.Combine(sessionDirectory, "session.json")));
        Assert.True(File.Exists(Path.Combine(entryDirectory, "entry.json")));
        Assert.True(File.Exists(Path.Combine(entryDirectory, "after", "records", $"{record.Id}.json")));
        Assert.True(await history.UndoAsync());
        Assert.Empty((await store.LoadAsync()).Records);
        Assert.True(await history.RedoAsync());
        Assert.Single((await store.LoadAsync()).Records);
        Assert.Empty(Directory.EnumerateDirectories(layout.TransactionsDirectory));

        await history.CompleteSessionAsync();
        Assert.False(Directory.Exists(sessionDirectory));
    }

    [Fact]
    public async Task CustomFieldDeletionUndoRestoresDefinitionAndRecordTogether()
    {
        using var temporary = new TemporaryDirectory();
        var layout = await InitializeAsync(temporary);
        var store = new JsonAssetManagerDataStore(layout);
        var fieldService = new FieldApplicationService(store);
        var field = await fieldService.AddCustomAsync("復元対象", FieldType.Text);
        var record = await new RecordApplicationService(store).CreateAsync(
            new Dictionary<FieldId, FieldValue?>
            {
                [field.Id] = new TextFieldValue("値"),
            });
        var persistence = new FileUndoHistoryPersistence(layout);
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        fieldService = new FieldApplicationService(store, history: history);

        await fieldService.DeleteCustomAsync(field.Id);
        var deleted = await store.LoadAsync();
        Assert.DoesNotContain(deleted.FieldDefinitions, item => item.Id == field.Id);
        Assert.False(deleted.Records.Single(item => item.Id == record.Id).Values.ContainsKey(field.Id));

        Assert.True(await history.UndoAsync());
        var restored = await store.LoadAsync();
        Assert.Contains(restored.FieldDefinitions, item => item.Id == field.Id);
        Assert.Equal(
            "値",
            Assert.IsType<TextFieldValue>(
                restored.Records.Single(item => item.Id == record.Id).Values[field.Id]).Value);
    }

    [Fact]
    public async Task RecordDeleteAndUndoNeverModifyManagedAssetFile()
    {
        using var temporary = new TemporaryDirectory();
        var assetPath = temporary.GetPath("managed-asset.txt");
        await File.WriteAllTextAsync(assetPath, "asset-content");
        var layout = await InitializeAsync(temporary);
        var store = new JsonAssetManagerDataStore(layout);
        var persistence = new FileUndoHistoryPersistence(layout);
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = new RecordApplicationService(store, history: history);
        var record = await records.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(TargetPathKind.File, assetPath),
        });

        await records.DeleteAsync(record.Id);
        Assert.True(File.Exists(assetPath));
        Assert.Equal("asset-content", await File.ReadAllTextAsync(assetPath));

        Assert.True(await history.UndoAsync());
        Assert.True(File.Exists(assetPath));
        Assert.Equal("asset-content", await File.ReadAllTextAsync(assetPath));
    }

    [Fact]
    public async Task NextStartupRemovesHistoryLeftByAbnormalTermination()
    {
        using var temporary = new TemporaryDirectory();
        var paths = new AppDataPaths(temporary.Path);
        var first = await new DataSetInitializer(paths).InitializeAsync();
        var layout = new DataRootLayout(first.DataRoot);
        var persistence = new FileUndoHistoryPersistence(layout);
        await persistence.InitializeAsync();
        var sessionDirectory = Path.Combine(layout.UndoDirectory, persistence.SessionId.ToString("D"));
        Assert.True(Directory.Exists(sessionDirectory));

        _ = await new DataSetInitializer(paths).InitializeAsync();

        Assert.False(Directory.Exists(sessionDirectory));
        Assert.Empty(Directory.EnumerateDirectories(layout.UndoDirectory));
    }

    private static async Task<DataRootLayout> InitializeAsync(TemporaryDirectory temporary)
    {
        var result = await new DataSetInitializer(new AppDataPaths(temporary.Path)).InitializeAsync();
        return new DataRootLayout(result.DataRoot);
    }
}
