namespace AssetManager.Infrastructure.Persistence.Models;

public sealed record BootstrapDocument(int SchemaVersion, string DataRoot);

public sealed record ManifestDocument(
    int SchemaVersion,
    string CatalogId,
    DateTimeOffset CreatedAt);

public sealed record AppSettingsDocument(
    int SchemaVersion,
    bool CheckPathsOnStartup,
    int LicenseWarningDays);

public sealed record ViewColumnDocument(
    string Id,
    int Order,
    double Width,
    bool Visible);

public sealed record SavedSearchConditionDocument(
    string FieldId,
    string Kind,
    string? Text = null,
    bool? Boolean = null,
    IReadOnlyList<string>? OptionIds = null);

public sealed record SavedSearchDocument(
    string Id,
    string Name,
    IReadOnlyList<SavedSearchConditionDocument> Conditions);

public sealed record ViewSettingsDocument(
    int SchemaVersion,
    IReadOnlyList<ViewColumnDocument> MainTableColumns,
    IReadOnlyList<SavedSearchDocument>? SavedSearches = null);
