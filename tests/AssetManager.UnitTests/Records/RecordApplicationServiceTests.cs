using AssetManager.Application.Data;
using AssetManager.Application.Records;
using AssetManager.Application.Paths;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.Records;

public sealed class RecordApplicationServiceTests
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
    public async Task CreateAllowsEmptyTargetPathAndReportsWarningState()
    {
        var store = CreateStore();
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));

        var record = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>());

        Assert.True(RecordApplicationService.HasMissingTargetPath(record));
        Assert.Equal(TestTime, record.CreatedAt);
        Assert.Equal(record, Assert.Single(store.Snapshot.Records));
    }

    [Fact]
    public async Task CreateAssignsEveryMatchingTypeForTargetFile()
    {
        var store = CreateStore(
            new AssetTypeDefinition(new AssetTypeId("type.audio"), "音声", ["wav"]),
            new AssetTypeDefinition(new AssetTypeId("type.raw-audio"), "非圧縮音声", [".WAV"]),
            new AssetTypeDefinition(new AssetTypeId("type.image"), "画像", ["png"]));
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));

        var record = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\sound.WAV"),
        });

        var types = record.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes);
        Assert.NotNull(types);
        Assert.Equal(["type.audio", "type.raw-audio"], types.Values.Select(id => id.Value));
    }

    [Fact]
    public async Task CreateDoesNotReplaceExplicitTypeOrAssignFolderType()
    {
        var type = new AssetTypeDefinition(new AssetTypeId("type.audio"), "音声", ["wav"]);
        var store = CreateStore(type);
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));

        var explicitRecord = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\sound.wav"),
            [BuiltInFieldIds.AssetTypes] = new AssetTypeSetFieldValue([type.Id]),
        });
        var folderRecord = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(
                TargetPathKind.Folder,
                @"C:\Assets\sound-folder.wav"),
        });

        Assert.Equal(
            [type.Id],
            explicitRecord.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes)!.Values);
        Assert.Null(folderRecord.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes));
    }

    [Fact]
    public async Task UpdateGetAndDeleteOperateOnStoredRecord()
    {
        var store = CreateStore();
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));
        var created = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("変更前"),
        });

        var updated = await service.UpdateAsync(created.Id, new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("変更後"),
        });

        Assert.Equal("変更後", updated.GetValue<TextFieldValue>(BuiltInFieldIds.Name)!.Value);
        Assert.Equal(updated, await service.GetAsync(created.Id));
        await service.DeleteAsync(created.Id);
        Assert.Null(await service.GetAsync(created.Id));
    }

    [Fact]
    public async Task UpdateRejectsUnknownField()
    {
        var store = CreateStore();
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));
        var created = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>());

        _ = await Assert.ThrowsAsync<DomainValidationException>(() => service.UpdateAsync(
            created.Id,
            new Dictionary<FieldId, FieldValue?>
            {
                [FieldId.From(CustomFieldId.New())] = new TextFieldValue("値"),
            }));
    }

    [Fact]
    public async Task DuplicateTargetPathUsesOrdinalIgnoreCaseComparison()
    {
        var store = CreateStore();
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));
        var first = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.Name] = new TextFieldValue("既存素材"),
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(
                TargetPathKind.File,
                @"C:\Assets\Image.PNG"),
        });

        var exception = await Assert.ThrowsAsync<DuplicateTargetPathException>(() => service.CreateAsync(
            new Dictionary<FieldId, FieldValue?>
            {
                [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(
                    TargetPathKind.File,
                    @"c:/assets/image.png"),
            }));

        Assert.Equal(first.Id.ToString(), exception.ConflictingRecordId);
        Assert.Equal("既存素材", exception.ConflictingRecordName);
        Assert.Single(store.Snapshot.Records);
    }

    [Fact]
    public async Task DeletedRecordDoesNotParticipateInDuplicateCheck()
    {
        var store = CreateStore();
        var service = new RecordApplicationService(store, new FixedTimeProvider(TestTime));
        var first = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(
                TargetPathKind.File,
                @"C:\Assets\Image.PNG"),
        });
        await service.DeleteAsync(first.Id);

        var replacement = await service.CreateAsync(new Dictionary<FieldId, FieldValue?>
        {
            [BuiltInFieldIds.TargetPath] = new TargetPathFieldValue(
                TargetPathKind.File,
                @"c:/assets/image.png"),
        });

        Assert.Equal(replacement, Assert.Single(store.Snapshot.Records));
    }

    private static TestDataStore CreateStore(params AssetTypeDefinition[] types)
    {
        return new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All,
            types,
            [],
            [],
            []));
    }
}
