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

public sealed record ViewSettingsDocument(
    int SchemaVersion,
    IReadOnlyList<ViewColumnDocument> MainTableColumns);
