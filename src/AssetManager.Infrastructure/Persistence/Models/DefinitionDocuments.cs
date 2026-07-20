using AssetManager.Domain.Fields;

namespace AssetManager.Infrastructure.Persistence.Models;

public sealed record SelectionOptionDocument(string Id, string Label);

public sealed record FieldDefinitionDocument(
    string Id,
    FieldOrigin Origin,
    SystemRole? SystemRole,
    string Label,
    FieldType Type,
    bool MainTableVisible,
    bool MainTableRequired,
    bool DetailVisible,
    bool UserCanHide,
    bool UserCanRename,
    bool UserCanChangeType,
    bool UserCanDelete,
    IReadOnlyList<SelectionOptionDocument> Options);

public sealed record FieldDefinitionsDocument(
    int SchemaVersion,
    IReadOnlyList<FieldDefinitionDocument> Fields);

public sealed record AssetTypeDocument(
    string Id,
    string Name,
    IReadOnlyList<string> Extensions);

public sealed record AssetTypesDocument(
    int SchemaVersion,
    IReadOnlyList<AssetTypeDocument> AssetTypes);

public sealed record LicenseTermsDocument(
    bool CreditRequired,
    bool LinkRequired,
    bool LogoRequired,
    bool CommercialUseAllowed,
    bool ModificationAllowed,
    bool RedistributionAllowed,
    bool AdultUseAllowed,
    bool GenerativeAiUseAllowed,
    bool ConditionsUnknown,
    bool NeedsReview);

public sealed record LicensePresetDocument(
    string Id,
    string Name,
    LicenseTermsDocument Terms);

public sealed record LicensePresetsDocument(
    int SchemaVersion,
    IReadOnlyList<LicensePresetDocument> LicensePresets);

public sealed record TagCategoryDocument(string Id, string Name);

public sealed record TagDocument(
    string Id,
    string Name,
    string Color,
    string? CategoryId);

public sealed record TagsDocument(
    int SchemaVersion,
    IReadOnlyList<TagCategoryDocument> Categories,
    IReadOnlyList<TagDocument> Tags);
