using AssetManager.Application.Data;
using AssetManager.Application.GridEditing;
using AssetManager.Application.History;
using AssetManager.Application.Records;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.GridEditing;

public sealed class GridClipboardServiceTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CopyAsyncは行列をTSVとして維持する()
    {
        var first = CreateRecord("素材A", 1200);
        var second = CreateRecord("素材B", 3400);
        var (store, service) = CreateService(first, second);

        var text = await service.CopyAsync(
            [first.Id, second.Id],
            [BuiltInFieldIds.Name, BuiltInFieldIds.PriceJpy]);

        Assert.Equal($"素材A\t1200{Environment.NewLine}素材B\t3400", text);
        Assert.Equal(0, store.TransactionSaveCount);
    }

    [Fact]
    public async Task PasteAsyncは1行を選択した複数行へ繰り返す()
    {
        var first = CreateRecord("素材A", 100);
        var second = CreateRecord("素材B", 200);
        var (store, service) = CreateService(first, second);

        var result = await service.PasteAsync(
            "共通名\t980",
            [first.Id, second.Id],
            [BuiltInFieldIds.Name, BuiltInFieldIds.PriceJpy]);

        Assert.Equal(2, result.Count);
        Assert.All(result, record => Assert.Equal(
            "共通名",
            record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value));
        Assert.All(result, record => Assert.Equal(
            980,
            record.GetValue<NumberFieldValue>(BuiltInFieldIds.PriceJpy)!.Value));
        Assert.Equal(1, store.TransactionSaveCount);
    }

    [Fact]
    public async Task PasteAsyncは複数行の行列を対応する行へ貼り付ける()
    {
        var first = CreateRecord("変更前A", 100);
        var second = CreateRecord("変更前B", 200);
        var (_, service) = CreateService(first, second);

        var result = await service.PasteAsync(
            "変更後A\t300\r\n変更後B\t400\r\n",
            [first.Id, second.Id],
            [BuiltInFieldIds.Name, BuiltInFieldIds.PriceJpy]);

        Assert.Equal(["変更後A", "変更後B"], result.Select(record =>
            record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value));
        Assert.Equal([300m, 400m], result.Select(record =>
            record.GetValue<NumberFieldValue>(BuiltInFieldIds.PriceJpy)!.Value));
    }

    [Fact]
    public async Task PasteAsyncは日付とチェック値を型付き値へ変換する()
    {
        var record = CreateRecord("素材", 100);
        var (_, service) = CreateService(record);

        var result = Assert.Single(await service.PasteAsync(
            "2026/07/19\tTRUE",
            [record.Id],
            [BuiltInFieldIds.AcquiredDate, BuiltInFieldIds.CommercialUseAllowed]));

        Assert.Equal(
            "2026/07/19",
            result.GetValue<DateFieldValue>(BuiltInFieldIds.AcquiredDate)!.Value.ToDisplayString());
        Assert.True(result.GetValue<BooleanFieldValue>(BuiltInFieldIds.CommercialUseAllowed)!.Value);
    }

    [Fact]
    public async Task PasteAsyncは行列不一致と不正な数値を保存前に拒否する()
    {
        var first = CreateRecord("素材A", 100);
        var second = CreateRecord("素材B", 200);
        var (store, service) = CreateService(first, second);

        _ = await Assert.ThrowsAsync<GridClipboardException>(() => service.PasteAsync(
            "1行目\n2行目\n3行目",
            [first.Id, second.Id],
            [BuiltInFieldIds.Name]));
        _ = await Assert.ThrowsAsync<GridClipboardException>(() => service.PasteAsync(
            "数値ではない値",
            [first.Id],
            [BuiltInFieldIds.PriceJpy]));

        Assert.Equal(0, store.TransactionSaveCount);
        Assert.Equal("素材A", store.Snapshot.Records[0]
            .GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value);
    }

    [Fact]
    public async Task PasteAsyncは対象パスへの貼り付けを拒否する()
    {
        var record = CreateRecord("素材", 100);
        var (store, service) = CreateService(record);

        var exception = await Assert.ThrowsAsync<GridClipboardException>(() => service.PasteAsync(
            @"C:\Assets\other.png",
            [record.Id],
            [BuiltInFieldIds.TargetPath]));

        Assert.Contains("対象パス", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.TransactionSaveCount);
    }

    [Fact]
    public async Task PasteAsyncは一操作としてアンドゥできる()
    {
        var first = CreateRecord("素材A", 100);
        var second = CreateRecord("素材B", 200);
        var store = CreateStore(first, second);
        var persistence = new TestUndoHistoryPersistence();
        await using var history = new UndoRedoService(store, persistence);
        await history.InitializeAsync();
        var records = new RecordApplicationService(
            store,
            new FixedTimeProvider(TestTime.AddMinutes(1)),
            history);
        var service = new GridClipboardService(store, records);

        _ = await service.PasteAsync(
            "変更後",
            [first.Id, second.Id],
            [BuiltInFieldIds.Name]);

        Assert.True(history.State.CanUndo);
        Assert.Single(persistence.Entries);
        Assert.True(await history.UndoAsync());
        Assert.Equal(
            ["素材A", "素材B"],
            store.Snapshot.Records.OrderBy(record => record.CreatedAt).Select(record =>
                record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value));
    }

    private static (TestDataStore Store, GridClipboardService Service) CreateService(
        params AssetRecord[] records)
    {
        var store = CreateStore(records);
        var recordService = new RecordApplicationService(
            store,
            new FixedTimeProvider(TestTime.AddMinutes(1)));
        return (store, new GridClipboardService(store, recordService));
    }

    private static TestDataStore CreateStore(params AssetRecord[] records)
    {
        return new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All,
            [],
            [],
            [],
            records));
    }

    private static AssetRecord CreateRecord(string name, decimal price)
    {
        var record = AssetRecord.Create(TestTime.AddTicks(price.GetHashCode()));
        record = record.SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Name),
            new TextFieldValue(name),
            record.UpdatedAt);
        return record.SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.PriceJpy),
            new NumberFieldValue(price),
            record.UpdatedAt);
    }
}
