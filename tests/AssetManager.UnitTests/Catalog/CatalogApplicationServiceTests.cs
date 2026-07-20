using AssetManager.Application.Catalog;
using AssetManager.Application.Data;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.Catalog;

public sealed class CatalogApplicationServiceTests
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
    public async Task AssetTypeCanBeAddedUpdatedAndDeletedWithNormalizedExtensions()
    {
        var store = CreateStore();
        var service = new CatalogApplicationService(store);

        var added = await service.AddAssetTypeAsync("音声", ["WAV", ".Mp3"]);
        var updated = await service.UpdateAssetTypeAsync(added.Id, "BGM", ["OGG"]);

        Assert.Equal([".wav", ".mp3"], added.Extensions);
        Assert.Equal("BGM", updated.Name);
        Assert.Equal([".ogg"], updated.Extensions);
        await service.DeleteAssetTypeAsync(added.Id);
        Assert.Empty(store.Snapshot.AssetTypes);
    }

    [Fact]
    public async Task UsedAssetTypeCannotBeDeleted()
    {
        var type = new AssetTypeDefinition(new AssetTypeId("type.audio"), "音声", ["wav"]);
        var record = AssetRecord.Create(TestTime).SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.AssetTypes),
            new AssetTypeSetFieldValue([type.Id]),
            TestTime);
        var store = CreateStore(assetTypes: [type], records: [record]);
        var service = new CatalogApplicationService(store);

        _ = await Assert.ThrowsAsync<CatalogItemInUseException>(
            () => service.DeleteAssetTypeAsync(type.Id));
    }

    [Fact]
    public async Task AcquisitionSourceCanBeAddedUpdatedAndDeleted()
    {
        var store = CreateStore();
        var service = new CatalogApplicationService(store);

        var added = await service.AddAcquisitionSourceAsync("BOOTH");
        var updated = await service.UpdateAcquisitionSourceAsync(added.Id, "BOOTHショップ");

        Assert.Equal("BOOTHショップ", updated.Label);
        var definition = store.Snapshot.FieldDefinitions.Single(
            field => field.Id == BuiltInFieldIds.AcquisitionSource);
        Assert.Equal(updated, Assert.Single(definition.Options));
        await service.DeleteAcquisitionSourceAsync(added.Id);
        Assert.Empty(store.Snapshot.FieldDefinitions.Single(
            field => field.Id == BuiltInFieldIds.AcquisitionSource).Options);
    }

    [Fact]
    public async Task UsedAcquisitionSourceCannotBeDeleted()
    {
        var source = new SelectionOption(new SelectionOptionId("acquisition-source.booth"), "BOOTH");
        var definitions = BuiltInFieldCatalog.All.Select(field =>
            field.Id == BuiltInFieldIds.AcquisitionSource
                ? field.SetSelectionOptions([source])
                : field).ToArray();
        var sourceDefinition = definitions.Single(field => field.Id == BuiltInFieldIds.AcquisitionSource);
        var record = AssetRecord.Create(TestTime).SetValue(
            sourceDefinition,
            new SingleSelectionFieldValue(source.Id),
            TestTime);
        var store = new TestDataStore(new AssetManagerDataSnapshot(
            definitions,
            [],
            [],
            [],
            [record]));
        var service = new CatalogApplicationService(store);

        _ = await Assert.ThrowsAsync<CatalogItemInUseException>(
            () => service.DeleteAcquisitionSourceAsync(source.Id));
    }

    [Fact]
    public async Task LicensePresetCanBeAddedUpdatedAndDeleted()
    {
        var store = CreateStore();
        var service = new CatalogApplicationService(store);
        var added = await service.AddLicensePresetAsync(
            "MIT",
            new LicenseTerms(
                CreditRequired: true,
                CommercialUseAllowed: true,
                ModificationAllowed: true,
                RedistributionAllowed: true));

        var updated = await service.UpdateLicensePresetAsync(
            added.Id,
            "MIT License",
            added.Terms with { LinkRequired = true });

        Assert.Equal("MIT License", updated.Name);
        Assert.True(updated.Terms.LinkRequired);
        Assert.Equal(
            updated.Id.Value,
            Assert.Single(store.Snapshot.FieldDefinitions.Single(
                field => field.Id == BuiltInFieldIds.LicensePreset).Options).Id.Value);

        await service.DeleteLicensePresetAsync(updated.Id);

        Assert.Empty(store.Snapshot.LicensePresets);
        Assert.Empty(store.Snapshot.FieldDefinitions.Single(
            field => field.Id == BuiltInFieldIds.LicensePreset).Options);
    }

    [Fact]
    public async Task UsedLicensePresetCannotBeDeleted()
    {
        var preset = new LicensePresetDefinition(
            new LicensePresetId("license-preset.mit"),
            "MIT",
            new LicenseTerms(CommercialUseAllowed: true));
        var option = new SelectionOption(new SelectionOptionId(preset.Id.Value), preset.Name);
        var definitions = BuiltInFieldCatalog.All.Select(field =>
            field.Id == BuiltInFieldIds.LicensePreset
                ? field.SetSelectionOptions([option])
                : field).ToArray();
        var presetDefinition = definitions.Single(field => field.Id == BuiltInFieldIds.LicensePreset);
        var record = AssetRecord.Create(TestTime).SetValue(
            presetDefinition,
            new SingleSelectionFieldValue(option.Id),
            TestTime);
        var store = new TestDataStore(new AssetManagerDataSnapshot(
            definitions,
            [],
            [],
            [],
            [record])
        {
            LicensePresets = [preset],
        });
        var service = new CatalogApplicationService(store);

        _ = await Assert.ThrowsAsync<CatalogItemInUseException>(
            () => service.DeleteLicensePresetAsync(preset.Id));
    }

    [Fact]
    public async Task TagCategoryAndTagCanBeAddedEditedAndDeleted()
    {
        var store = CreateStore();
        var service = new CatalogApplicationService(store);
        var category = await service.AddTagCategoryAsync("ジャンル");
        var tag = await service.AddTagAsync("ファンタジー", new TagColor("#123456"), category.Id);

        var updatedCategory = await service.UpdateTagCategoryAsync(category.Id, "世界観");
        var updatedTag = await service.UpdateTagAsync(
            tag.Id,
            "SF",
            new TagColor("#ABCDEF"),
            category.Id);

        Assert.Equal("世界観", updatedCategory.Name);
        Assert.Equal("SF", updatedTag.Name);
        _ = await Assert.ThrowsAsync<CatalogItemInUseException>(
            () => service.DeleteTagCategoryAsync(category.Id));
        await service.DeleteTagAsync(tag.Id);
        await service.DeleteTagCategoryAsync(category.Id);
        Assert.Empty(store.Snapshot.Tags);
        Assert.Empty(store.Snapshot.TagCategories);
    }

    [Fact]
    public async Task UsedTagCannotBeDeleted()
    {
        var tag = new TagDefinition(new TagId("tag.favorite"), "お気に入り", new TagColor("#123456"));
        var record = AssetRecord.Create(TestTime).SetValue(
            BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Tags),
            new TagSetFieldValue([tag.Id]),
            TestTime);
        var store = CreateStore(tags: [tag], records: [record]);
        var service = new CatalogApplicationService(store);

        _ = await Assert.ThrowsAsync<CatalogItemInUseException>(() => service.DeleteTagAsync(tag.Id));
    }

    private static TestDataStore CreateStore(
        IReadOnlyList<AssetTypeDefinition>? assetTypes = null,
        IReadOnlyList<TagCategoryDefinition>? categories = null,
        IReadOnlyList<TagDefinition>? tags = null,
        IReadOnlyList<AssetRecord>? records = null,
        IReadOnlyList<LicensePresetDefinition>? licensePresets = null)
    {
        return new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All,
            assetTypes ?? [],
            categories ?? [],
            tags ?? [],
            records ?? [])
        {
            LicensePresets = licensePresets ?? [],
        });
    }
}
