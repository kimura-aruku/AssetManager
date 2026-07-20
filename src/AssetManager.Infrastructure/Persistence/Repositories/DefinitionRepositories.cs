using System.Text.Json;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Validation;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Models;

namespace AssetManager.Infrastructure.Persistence.Repositories;

public sealed class FieldDefinitionRepository(AtomicJsonFileStore store)
{
    public async Task<IReadOnlyList<FieldDefinition>> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await store.ReadAsync<FieldDefinitionsDocument>(
                layout.FieldsFile,
                ValidateDocument,
                cancellationToken).ConfigureAwait(false);
            return document.Fields.Select(ToDomain).ToArray();
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException or ArgumentException)
        {
            throw new CriticalDataFileException(layout.FieldsFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        IEnumerable<FieldDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var document = CreateDocument(definitions);
        return store.SaveAsync(layout.FieldsFile, document, ValidateDocument, cancellationToken);
    }

    public static FieldDefinitionsDocument CreateDocument(
        IEnumerable<FieldDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new FieldDefinitionsDocument(
            JsonDefaults.CurrentSchemaVersion,
            definitions.Select(ToDocument).ToArray());
    }

    private static void ValidateDocument(FieldDefinitionsDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        var definitions = document.Fields.Select(ToDomain).ToArray();
        var issues = DomainModelValidator.ValidateFieldDefinitions(definitions);
        if (issues.Count > 0)
        {
            throw new DataPersistenceException(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        }
    }

    private static FieldDefinition ToDomain(FieldDefinitionDocument document)
    {
        var options = document.Options.Select(option => new SelectionOption(
            new SelectionOptionId(option.Id),
            option.Label));
        FieldDefinition definition;

        if (document.Origin == FieldOrigin.BuiltIn)
        {
            if (document.SystemRole is null)
            {
                throw new DataPersistenceException("標準カラムにはsystemRoleが必要です。");
            }

            definition = FieldDefinition.CreateBuiltIn(
                new FieldId(document.Id),
                document.Label,
                document.Type,
                document.SystemRole.Value,
                document.MainTableVisible,
                document.MainTableRequired,
                document.DetailVisible,
                document.UserCanHide,
                options);
        }
        else
        {
            definition = FieldDefinition.CreateCustom(
                CustomFieldId.Parse(document.Id),
                document.Label,
                document.Type,
                document.MainTableVisible,
                document.DetailVisible,
                options);
        }

        if (definition.UserCanHide != document.UserCanHide
            || definition.UserCanRename != document.UserCanRename
            || definition.UserCanChangeType != document.UserCanChangeType
            || definition.UserCanDelete != document.UserCanDelete)
        {
            throw new DataPersistenceException($"カラム'{document.Id}'の変更可否設定が正しくありません。");
        }

        return definition;
    }

    private static FieldDefinitionDocument ToDocument(FieldDefinition definition)
    {
        return new FieldDefinitionDocument(
            definition.Id.Value,
            definition.Origin,
            definition.SystemRole,
            definition.Label,
            definition.Type,
            definition.MainTableVisible,
            definition.MainTableRequired,
            definition.DetailVisible,
            definition.UserCanHide,
            definition.UserCanRename,
            definition.UserCanChangeType,
            definition.UserCanDelete,
            definition.Options
                .Select(option => new SelectionOptionDocument(option.Id.Value, option.Label))
                .ToArray());
    }
}

public sealed class AssetTypeRepository(AtomicJsonFileStore store)
{
    public async Task<IReadOnlyList<AssetTypeDefinition>> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await store.ReadAsync<AssetTypesDocument>(
                layout.AssetTypesFile,
                ValidateDocument,
                cancellationToken).ConfigureAwait(false);
            return document.AssetTypes.Select(ToDomain).ToArray();
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException or ArgumentException)
        {
            throw new CriticalDataFileException(layout.AssetTypesFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        IEnumerable<AssetTypeDefinition> assetTypes,
        CancellationToken cancellationToken = default)
    {
        var document = new AssetTypesDocument(
            JsonDefaults.CurrentSchemaVersion,
            assetTypes.Select(type => new AssetTypeDocument(type.Id.Value, type.Name, type.Extensions)).ToArray());
        return store.SaveAsync(layout.AssetTypesFile, document, ValidateDocument, cancellationToken);
    }

    private static void ValidateDocument(AssetTypesDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        var types = document.AssetTypes.Select(ToDomain).ToArray();
        if (types.Select(type => type.Id).Distinct().Count() != types.Length)
        {
            throw new DataPersistenceException("種類IDが重複しています。");
        }
    }

    private static AssetTypeDefinition ToDomain(AssetTypeDocument document)
    {
        return new AssetTypeDefinition(
            new AssetTypeId(document.Id),
            document.Name,
            document.Extensions);
    }
}

public sealed class LicensePresetRepository(AtomicJsonFileStore store)
{
    public static void ValidateFieldOptions(
        IEnumerable<FieldDefinition> fieldDefinitions,
        IEnumerable<LicensePresetDefinition> licensePresets)
    {
        var definition = fieldDefinitions.FirstOrDefault(
            field => field.Id == BuiltInFieldIds.LicensePreset);
        if (definition is null)
        {
            return;
        }

        var presets = licensePresets.ToDictionary(preset => preset.Id.Value, StringComparer.Ordinal);
        var matches = definition.Options.Count == presets.Count
            && definition.Options.All(option => presets.TryGetValue(option.Id.Value, out var preset)
                && string.Equals(option.Label, preset.Name, StringComparison.Ordinal));
        if (!matches)
        {
            throw new DataPersistenceException("定型ライセンスマスタとカラム選択肢が一致していません。");
        }
    }

    public async Task<IReadOnlyList<LicensePresetDefinition>> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await store.ReadAsync<LicensePresetsDocument>(
                layout.LicensePresetsFile,
                ValidateDocument,
                cancellationToken).ConfigureAwait(false);
            return document.LicensePresets.Select(ToDomain).ToArray();
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException or ArgumentException)
        {
            throw new CriticalDataFileException(layout.LicensePresetsFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        IEnumerable<LicensePresetDefinition> licensePresets,
        CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(
            layout.LicensePresetsFile,
            CreateDocument(licensePresets),
            ValidateDocument,
            cancellationToken);
    }

    public static LicensePresetsDocument CreateDocument(
        IEnumerable<LicensePresetDefinition> licensePresets)
    {
        ArgumentNullException.ThrowIfNull(licensePresets);
        return new LicensePresetsDocument(
            JsonDefaults.CurrentSchemaVersion,
            licensePresets.Select(ToDocument).ToArray());
    }

    private static void ValidateDocument(LicensePresetsDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        var presets = document.LicensePresets.Select(ToDomain).ToArray();
        if (presets.Select(preset => preset.Id).Distinct().Count() != presets.Length)
        {
            throw new DataPersistenceException("定型ライセンスIDが重複しています。");
        }

        if (presets.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != presets.Length)
        {
            throw new DataPersistenceException("定型ライセンス名が重複しています。");
        }
    }

    private static LicensePresetDefinition ToDomain(LicensePresetDocument document)
    {
        return new LicensePresetDefinition(
            new LicensePresetId(document.Id),
            document.Name,
            new LicenseTerms(
                document.Terms.CommercialUseAllowed,
                document.Terms.ModificationAllowed,
                document.Terms.ProductEmbeddingAllowed,
                document.Terms.OriginalDataRedistributionAllowed
                    || document.Terms.RedistributionAllowed is true,
                document.Terms.CreditDisplayRequired
                    || document.Terms.CreditRequired is true,
                document.Terms.CopyrightNoticeRetentionRequired,
                document.Terms.LicenseTextAttachmentRequired,
                document.Terms.SameLicenseRequired,
                document.Terms.AiTrainingAllowed,
                document.Terms.GenerativeAiInputAllowed
                    || document.Terms.GenerativeAiUseAllowed is true,
                document.Terms.EngineRestrictionExists,
                document.Terms.NeedsReview));
    }

    private static LicensePresetDocument ToDocument(LicensePresetDefinition preset)
    {
        return new LicensePresetDocument(
            preset.Id.Value,
            preset.Name,
            new LicenseTermsDocument(
                preset.Terms.CommercialUseAllowed,
                preset.Terms.ModificationAllowed,
                preset.Terms.ProductEmbeddingAllowed,
                preset.Terms.OriginalDataRedistributionAllowed,
                preset.Terms.CreditDisplayRequired,
                preset.Terms.CopyrightNoticeRetentionRequired,
                preset.Terms.LicenseTextAttachmentRequired,
                preset.Terms.SameLicenseRequired,
                preset.Terms.AiTrainingAllowed,
                preset.Terms.GenerativeAiInputAllowed,
                preset.Terms.EngineRestrictionExists,
                preset.Terms.NeedsReview));
    }
}

public sealed record TagCatalog(
    IReadOnlyList<TagCategoryDefinition> Categories,
    IReadOnlyList<TagDefinition> Tags);

public sealed class TagRepository(AtomicJsonFileStore store)
{
    public async Task<TagCatalog> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await store.ReadAsync<TagsDocument>(
                layout.TagsFile,
                ValidateDocument,
                cancellationToken).ConfigureAwait(false);
            return ToDomain(document);
        }
        catch (Exception exception) when (exception is IOException or JsonException or DataPersistenceException or ArgumentException)
        {
            throw new CriticalDataFileException(layout.TagsFile, exception);
        }
    }

    public Task SaveAsync(
        DataRootLayout layout,
        TagCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var document = new TagsDocument(
            JsonDefaults.CurrentSchemaVersion,
            catalog.Categories
                .Select(category => new TagCategoryDocument(category.Id.Value, category.Name))
                .ToArray(),
            catalog.Tags
                .Select(tag => new TagDocument(
                    tag.Id.Value,
                    tag.Name,
                    tag.Color.Value,
                    tag.CategoryId?.Value))
                .ToArray());
        return store.SaveAsync(layout.TagsFile, document, ValidateDocument, cancellationToken);
    }

    private static void ValidateDocument(TagsDocument document)
    {
        SchemaVersionGuard.EnsureCurrent(document.SchemaVersion);
        _ = ToDomain(document);
    }

    private static TagCatalog ToDomain(TagsDocument document)
    {
        var categories = document.Categories
            .Select(category => new TagCategoryDefinition(
                new TagCategoryId(category.Id),
                category.Name))
            .ToArray();
        var tags = document.Tags
            .Select(tag => new TagDefinition(
                new TagId(tag.Id),
                tag.Name,
                new TagColor(tag.Color),
                tag.CategoryId is null ? null : new TagCategoryId(tag.CategoryId)))
            .ToArray();

        if (categories.Select(category => category.Id).Distinct().Count() != categories.Length
            || tags.Select(tag => tag.Id).Distinct().Count() != tags.Length)
        {
            throw new DataPersistenceException("タグまたはタグ分類のIDが重複しています。");
        }

        var issues = DomainModelValidator.ValidateTagCategories(tags, categories);
        if (issues.Count > 0)
        {
            throw new DataPersistenceException(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        }

        return new TagCatalog(categories, tags);
    }
}
