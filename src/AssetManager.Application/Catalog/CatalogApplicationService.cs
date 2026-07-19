using AssetManager.Application.Data;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Catalog;

public sealed class CatalogItemInUseException(string message) : InvalidOperationException(message);

public sealed class CatalogApplicationService(IAssetManagerDataStore store)
{
    private readonly IAssetManagerDataStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<SelectionOption> AddAcquisitionSourceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = GetAcquisitionSourceDefinition(snapshot.FieldDefinitions);
        EnsureUniqueAcquisitionSourceName(definition.Options, normalizedName);
        var option = new SelectionOption(
            new SelectionOptionId($"acquisition-source.{Guid.CreateVersion7():D}"),
            normalizedName);
        await SaveAcquisitionSourceDefinitionAsync(
            snapshot.FieldDefinitions,
            definition.SetSelectionOptions(definition.Options.Append(option)),
            cancellationToken).ConfigureAwait(false);
        return option;
    }

    public async Task<SelectionOption> UpdateAcquisitionSourceAsync(
        SelectionOptionId id,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = GetAcquisitionSourceDefinition(snapshot.FieldDefinitions);
        var current = definition.Options.FirstOrDefault(option => option.Id == id)
            ?? throw new KeyNotFoundException($"購入／入手元'{id}'が見つかりません。");
        EnsureUniqueAcquisitionSourceName(definition.Options, normalizedName, id);
        var updated = new SelectionOption(current.Id, normalizedName);
        await SaveAcquisitionSourceDefinitionAsync(
            snapshot.FieldDefinitions,
            definition.SetSelectionOptions(
                definition.Options.Select(option => option.Id == id ? updated : option)),
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteAcquisitionSourceAsync(
        SelectionOptionId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = GetAcquisitionSourceDefinition(snapshot.FieldDefinitions);
        if (definition.Options.All(option => option.Id != id))
        {
            throw new KeyNotFoundException($"購入／入手元'{id}'が見つかりません。");
        }

        if (snapshot.Records.Any(record =>
                record.GetValue<SingleSelectionFieldValue>(BuiltInFieldIds.AcquisitionSource)?.Value == id))
        {
            throw new CatalogItemInUseException("レコードで使用中の購入／入手元は削除できません。");
        }

        await SaveAcquisitionSourceDefinitionAsync(
            snapshot.FieldDefinitions,
            definition.SetSelectionOptions(definition.Options.Where(option => option.Id != id)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetTypeDefinition> AddAssetTypeAsync(
        string name,
        IEnumerable<string>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var definition = new AssetTypeDefinition(
            new AssetTypeId($"type.{Guid.CreateVersion7():D}"),
            name,
            extensions);
        await _store.SaveAssetTypesAsync(
            snapshot.AssetTypes.Append(definition).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return definition;
    }

    public async Task<AssetTypeDefinition> UpdateAssetTypeAsync(
        AssetTypeId id,
        string name,
        IEnumerable<string>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.AssetTypes, id);
        var updated = new AssetTypeDefinition(id, name, extensions);
        await _store.SaveAssetTypesAsync(
            snapshot.AssetTypes.Select(item => item.Id == id ? updated : item).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteAssetTypeAsync(
        AssetTypeId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.AssetTypes, id);
        if (snapshot.Records.Any(record =>
                record.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes)?.Values.Contains(id) == true))
        {
            throw new CatalogItemInUseException("レコードで使用中の種類は削除できません。");
        }

        await _store.SaveAssetTypesAsync(
            snapshot.AssetTypes.Where(item => item.Id != id).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TagCategoryDefinition> AddTagCategoryAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var category = new TagCategoryDefinition(
            new TagCategoryId($"tag-category.{Guid.CreateVersion7():D}"),
            name);
        await _store.SaveTagsAsync(
            snapshot.TagCategories.Append(category).ToArray(),
            snapshot.Tags,
            cancellationToken).ConfigureAwait(false);
        return category;
    }

    public async Task<TagCategoryDefinition> UpdateTagCategoryAsync(
        TagCategoryId id,
        string name,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.TagCategories, id);
        var updated = new TagCategoryDefinition(id, name);
        await _store.SaveTagsAsync(
            snapshot.TagCategories.Select(item => item.Id == id ? updated : item).ToArray(),
            snapshot.Tags,
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteTagCategoryAsync(
        TagCategoryId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.TagCategories, id);
        if (snapshot.Tags.Any(tag => tag.CategoryId == id))
        {
            throw new CatalogItemInUseException("タグで使用中の分類は削除できません。");
        }

        await _store.SaveTagsAsync(
            snapshot.TagCategories.Where(item => item.Id != id).ToArray(),
            snapshot.Tags,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TagDefinition> AddTagAsync(
        string name,
        TagColor color,
        TagCategoryId? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureCategoryExists(snapshot.TagCategories, categoryId);
        var tag = new TagDefinition(
            new TagId($"tag.{Guid.CreateVersion7():D}"),
            name,
            color,
            categoryId);
        await _store.SaveTagsAsync(
            snapshot.TagCategories,
            snapshot.Tags.Append(tag).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return tag;
    }

    public async Task<TagDefinition> UpdateTagAsync(
        TagId id,
        string name,
        TagColor color,
        TagCategoryId? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.Tags, id);
        EnsureCategoryExists(snapshot.TagCategories, categoryId);
        var updated = new TagDefinition(id, name, color, categoryId);
        await _store.SaveTagsAsync(
            snapshot.TagCategories,
            snapshot.Tags.Select(item => item.Id == id ? updated : item).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task DeleteTagAsync(
        TagId id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        EnsureExists(snapshot.Tags, id);
        if (snapshot.Records.Any(record =>
                record.GetValue<TagSetFieldValue>(BuiltInFieldIds.Tags)?.Values.Contains(id) == true))
        {
            throw new CatalogItemInUseException("レコードで使用中のタグは削除できません。");
        }

        await _store.SaveTagsAsync(
            snapshot.TagCategories,
            snapshot.Tags.Where(item => item.Id != id).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureCategoryExists(
        IReadOnlyList<TagCategoryDefinition> categories,
        TagCategoryId? id)
    {
        if (id is not null && categories.All(category => category.Id != id))
        {
            throw new DomainValidationException($"タグ分類'{id}'が見つかりません。", nameof(id));
        }
    }

    private static void EnsureExists(
        IReadOnlyList<AssetTypeDefinition> definitions,
        AssetTypeId id)
    {
        if (definitions.All(item => item.Id != id))
        {
            throw new KeyNotFoundException($"種類'{id}'が見つかりません。");
        }
    }

    private static void EnsureExists(
        IReadOnlyList<TagCategoryDefinition> definitions,
        TagCategoryId id)
    {
        if (definitions.All(item => item.Id != id))
        {
            throw new KeyNotFoundException($"タグ分類'{id}'が見つかりません。");
        }
    }

    private static void EnsureExists(IReadOnlyList<TagDefinition> definitions, TagId id)
    {
        if (definitions.All(item => item.Id != id))
        {
            throw new KeyNotFoundException($"タグ'{id}'が見つかりません。");
        }
    }

    private static FieldDefinition GetAcquisitionSourceDefinition(
        IReadOnlyList<FieldDefinition> definitions)
    {
        var definition = definitions.FirstOrDefault(
            field => field.Id == BuiltInFieldIds.AcquisitionSource)
            ?? throw new KeyNotFoundException("購入／入手元カラムが見つかりません。");
        if (definition.Type != FieldType.SingleSelect)
        {
            throw new DomainValidationException("購入／入手元カラムのデータ移行が完了していません。");
        }

        return definition;
    }

    private static void EnsureUniqueAcquisitionSourceName(
        IEnumerable<SelectionOption> options,
        string name,
        SelectionOptionId? exceptId = null)
    {
        if (options.Any(option => option.Id != exceptId
                && string.Equals(option.Label, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainValidationException($"購入／入手元'{name}'は既に登録されています。", nameof(name));
        }
    }

    private Task SaveAcquisitionSourceDefinitionAsync(
        IReadOnlyList<FieldDefinition> definitions,
        FieldDefinition updated,
        CancellationToken cancellationToken)
    {
        return _store.SaveFieldDefinitionsAsync(
            definitions.Select(definition => definition.Id == updated.Id ? updated : definition).ToArray(),
            cancellationToken);
    }
}
