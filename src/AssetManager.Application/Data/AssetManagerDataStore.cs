using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Application.History;

namespace AssetManager.Application.Data;

public sealed record AssetManagerDataSnapshot(
    IReadOnlyList<FieldDefinition> FieldDefinitions,
    IReadOnlyList<AssetTypeDefinition> AssetTypes,
    IReadOnlyList<TagCategoryDefinition> TagCategories,
    IReadOnlyList<TagDefinition> Tags,
    IReadOnlyList<AssetRecord> Records);

public interface IAssetManagerDataStore
{
    Task<AssetManagerDataSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveRecordAsync(
        AssetRecord record,
        CancellationToken cancellationToken = default);

    Task DeleteRecordAsync(
        RecordId recordId,
        CancellationToken cancellationToken = default);

    Task SaveFieldDefinitionsAsync(
        IReadOnlyList<FieldDefinition> definitions,
        CancellationToken cancellationToken = default);

    Task SaveFieldsAndRecordsAsync(
        IReadOnlyList<FieldDefinition> originalDefinitions,
        IReadOnlyList<FieldDefinition> updatedDefinitions,
        IReadOnlyList<AssetRecord> updatedRecords,
        CancellationToken cancellationToken = default);

    Task SaveAssetTypesAsync(
        IReadOnlyList<AssetTypeDefinition> assetTypes,
        CancellationToken cancellationToken = default);

    Task SaveTagsAsync(
        IReadOnlyList<TagCategoryDefinition> categories,
        IReadOnlyList<TagDefinition> tags,
        CancellationToken cancellationToken = default);

    Task ApplyDataChangeAsync(
        UndoableDataChange change,
        bool useAfter,
        CancellationToken cancellationToken = default);
}
