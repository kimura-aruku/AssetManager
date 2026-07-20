using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.IntegrationTests.Persistence;

public sealed class CoreRepositoryTests
{
    [Fact]
    public async Task BootstrapCreatesDefaultDataRoot()
    {
        using var temporary = new TemporaryDirectory();
        var paths = new AppDataPaths(temporary.Path);
        var repository = new BootstrapRepository(new AtomicJsonFileStore());

        var layout = await repository.LoadOrCreateAsync(paths);

        Assert.Equal(Path.GetFullPath(paths.DefaultDataRoot), layout.RootDirectory);
        Assert.True(File.Exists(paths.BootstrapFile));
    }

    [Fact]
    public async Task DefinitionRepositoriesRoundTripDomainModels()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        var fields = new FieldDefinitionRepository(store);
        var types = new AssetTypeRepository(store);
        var licensePresets = new LicensePresetRepository(store);
        var tags = new TagRepository(store);
        var custom = FieldDefinition.CreateCustom(CustomFieldId.New(), "自由欄", FieldType.Text);
        var type = new AssetTypeDefinition(new AssetTypeId("type.bgm"), "BGM", ["wav"]);
        var category = new TagCategoryDefinition(new TagCategoryId("tag-category.genre"), "ジャンル");
        var tag = new TagDefinition(
            new TagId("tag.fantasy"),
            "ファンタジー",
            new TagColor("#123456"),
            category.Id);
        var preset = new LicensePresetDefinition(
            new LicensePresetId("license-preset.mit"),
            "MIT",
            new LicenseTerms(CommercialUseAllowed: true, ModificationAllowed: true));

        await fields.SaveAsync(layout, BuiltInFieldCatalog.All.Append(custom));
        await types.SaveAsync(layout, [type]);
        await licensePresets.SaveAsync(layout, [preset]);
        await tags.SaveAsync(layout, new TagCatalog([category], [tag]));

        var loadedFields = await fields.LoadAsync(layout);
        var loadedTypes = await types.LoadAsync(layout);
        var loadedPresets = await licensePresets.LoadAsync(layout);
        var loadedTags = await tags.LoadAsync(layout);
        Assert.Contains(loadedFields, field => field.Id == custom.Id);
        Assert.Equal(".wav", Assert.Single(loadedTypes).Extensions[0]);
        Assert.True(Assert.Single(loadedPresets).Terms.CommercialUseAllowed);
        Assert.Equal(category.Id, Assert.Single(loadedTags.Tags).CategoryId);
    }

    [Fact]
    public async Task LegacyLicensePresetTermsAreMappedToNewConditions()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        var document = new LicensePresetsDocument(
            1,
            [
                new LicensePresetDocument(
                    "license-preset.legacy",
                    "旧定型ライセンス",
                    new LicenseTermsDocument(
                        CommercialUseAllowed: true,
                        ModificationAllowed: true,
                        NeedsReview: true,
                        CreditRequired: true,
                        RedistributionAllowed: true,
                        GenerativeAiUseAllowed: true)),
            ]);
        await store.SaveAsync(layout.LicensePresetsFile, document);

        var preset = Assert.Single(await new LicensePresetRepository(store).LoadAsync(layout));

        Assert.True(preset.Terms.CommercialUseAllowed);
        Assert.True(preset.Terms.ModificationAllowed);
        Assert.True(preset.Terms.CreditDisplayRequired);
        Assert.True(preset.Terms.OriginalDataRedistributionAllowed);
        Assert.True(preset.Terms.GenerativeAiInputAllowed);
        Assert.True(preset.Terms.NeedsReview);
    }

    [Fact]
    public async Task CorruptCriticalDefinitionStopsLoading()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        await File.WriteAllTextAsync(layout.FieldsFile, "{ broken");
        var repository = new FieldDefinitionRepository(new AtomicJsonFileStore());

        var exception = await Assert.ThrowsAsync<CriticalDataFileException>(
            () => repository.LoadAsync(layout));

        Assert.Equal(layout.FieldsFile, exception.Path);
    }

    [Fact]
    public async Task SettingsAndViewsRoundTrip()
    {
        using var temporary = new TemporaryDirectory();
        var layout = new DataRootLayout(temporary.Path);
        layout.EnsureDirectories();
        var store = new AtomicJsonFileStore();
        var settings = new SettingsRepository(store);
        var views = new ViewSettingsRepository(store);
        var settingsDocument = new AppSettingsDocument(1, true, 365);
        var viewsDocument = new ViewSettingsDocument(
            1,
            [new ViewColumnDocument("builtin.name", 0, 240, true)]);

        await settings.SaveAsync(layout, settingsDocument);
        await views.SaveAsync(layout, viewsDocument);

        Assert.Equal(settingsDocument, await settings.LoadAsync(layout));
        var loadedViews = await views.LoadAsync(layout);
        Assert.Equal("builtin.name", Assert.Single(loadedViews.MainTableColumns).Id);
    }
}
