using AssetManager.Application.Data;
using AssetManager.Application.Fields;
using AssetManager.Application.History;
using AssetManager.Application.Records;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.History;

public sealed class UndoRedoServiceTests
{
    private static readonly DateTimeOffset TestTime = new(
        2026,
        7,
        19,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task RecordCreateEditDeleteCanBeUndoneAndRedone()
    {
        var store = CreateStore();
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = CreateRecordService(store, history);

        var created = await records.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("作成"),
        });
        _ = await records.UpdateAsync(created.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("編集"),
        });
        await records.DeleteAsync(created.Id);

        Assert.Empty(store.Snapshot.Records);
        Assert.Equal(3, persistence.Entries.Count);
        Assert.True(await history.UndoAsync());
        Assert.Equal("編集", GetName(store, created.Id));
        Assert.True(await history.UndoAsync());
        Assert.Equal("作成", GetName(store, created.Id));
        Assert.True(await history.UndoAsync());
        Assert.Empty(store.Snapshot.Records);
        Assert.False(await history.UndoAsync());

        Assert.True(await history.RedoAsync());
        Assert.Equal("作成", GetName(store, created.Id));
        Assert.True(await history.RedoAsync());
        Assert.Equal("編集", GetName(store, created.Id));
        Assert.True(await history.RedoAsync());
        Assert.Empty(store.Snapshot.Records);
    }

    [Fact]
    public async Task BulkEditIsStoredAsOneOperation()
    {
        var first = CreateNamedRecord("A");
        var second = CreateNamedRecord("B");
        var store = CreateStore([first, second]);
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = CreateRecordService(store, history);

        _ = await records.UpdateManyAsync(new Dictionary<RecordId, IReadOnlyDictionary<FieldId, FieldValue?>>
        {
            [first.Id] = new Dictionary<FieldId, FieldValue?>
            {
                [BuiltInFieldIds.Name] = new TextFieldValue("一括"),
            },
            [second.Id] = new Dictionary<FieldId, FieldValue?>
            {
                [BuiltInFieldIds.Name] = new TextFieldValue("一括"),
            },
        }, "貼り付け");

        Assert.Single(persistence.Entries);
        Assert.All(store.Snapshot.Records, record => Assert.Equal("一括", GetName(record)));
        Assert.True(await history.UndoAsync());
        Assert.Equal(["A", "B"], store.Snapshot.Records.Select(GetName).Order());
    }

    [Fact]
    public async Task NewOperationAfterUndoDiscardsRedoBranch()
    {
        var record = CreateNamedRecord("初期");
        var store = CreateStore([record]);
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = CreateRecordService(store, history);
        _ = await records.UpdateAsync(record.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("変更1"),
        });
        Assert.True(await history.UndoAsync());

        _ = await records.UpdateAsync(record.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("変更2"),
        });

        Assert.False(history.State.CanRedo);
        Assert.False(await history.RedoAsync());
        Assert.Single(persistence.Entries);
        Assert.Equal("変更2", GetName(store, record.Id));
    }

    [Fact]
    public async Task ConstraintFailureKeepsHistoryPosition()
    {
        var path = new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\same.png");
        var deleted = AssetRecord.Create(TestTime).SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.TargetPath),
            path,
            TestTime);
        var store = CreateStore([deleted]);
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = CreateRecordService(store, history);
        await records.DeleteAsync(deleted.Id);
        var conflicting = AssetRecord.Create(TestTime).SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.TargetPath),
            path,
            TestTime);
        await store.ApplyDataChangeAsync(
            new UndoableDataChange(
                "外部作成",
                [new RecordStateChange(conflicting.Id, null, conflicting)]),
            useAfter: true);

        _ = await Assert.ThrowsAsync<HistoryConstraintException>(() => history.UndoAsync());

        Assert.True(history.State.CanUndo);
        Assert.False(history.State.CanRedo);
        Assert.Equal(1, persistence.Cursor);
        Assert.Single(store.Snapshot.Records);
    }

    [Fact]
    public async Task CustomFieldDeletionRestoresDefinitionAndValuesAsOneOperation()
    {
        var field = FieldDefinition.CreateCustom(CustomFieldId.New(), "削除欄", FieldType.Text);
        var record = AssetRecord.Create(TestTime).SetValue(field, new TextFieldValue("値"), TestTime);
        var store = CreateStore([record], [field]);
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var fields = new FieldApplicationService(store, new FixedTimeProvider(TestTime), history);

        await fields.DeleteCustomAsync(field.Id);

        Assert.Single(persistence.Entries);
        Assert.DoesNotContain(store.Snapshot.FieldDefinitions, item => item.Id == field.Id);
        Assert.True(await history.UndoAsync());
        Assert.Contains(store.Snapshot.FieldDefinitions, item => item.Id == field.Id);
        Assert.True(Assert.Single(store.Snapshot.Records).Values.ContainsKey(field.Id));
    }

    [Fact]
    public async Task NormalCompletionDeletesSessionHistory()
    {
        var store = CreateStore();
        var persistence = new TestUndoHistoryPersistence();
        var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        _ = await CreateRecordService(store, history).CreateAsync(
            new Dictionary<FieldId, FieldValue?>());

        await history.CompleteSessionAsync();

        Assert.True(persistence.SessionDeleted);
        Assert.Empty(persistence.Entries);
        Assert.False(history.State.CanUndo);
        await history.DisposeAsync();
    }

    private static RecordApplicationService CreateRecordService(
        TestDataStore store,
        UndoRedoService history)
    {
        return new RecordApplicationService(store, new FixedTimeProvider(TestTime), history);
    }

    private static TestDataStore CreateStore(
        IReadOnlyList<AssetRecord>? records = null,
        IReadOnlyList<FieldDefinition>? customFields = null)
    {
        return new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All.Concat(customFields ?? []).ToArray(),
            [],
            [],
            [],
            records ?? []));
    }

    private static AssetRecord CreateNamedRecord(string name)
    {
        return AssetRecord.Create(TestTime).SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Name),
            new TextFieldValue(name),
            TestTime);
    }

    private static string GetName(TestDataStore store, RecordId id)
    {
        return GetName(store.Snapshot.Records.Single(record => record.Id == id));
    }

    private static string GetName(AssetRecord record)
    {
        return record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value;
    }
}
