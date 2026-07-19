using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Infrastructure.Persistence.Migrations;
using AssetManager.Infrastructure.Persistence.Models;
using AssetManager.Infrastructure.Persistence.Recovery;
using AssetManager.Infrastructure.Persistence.Transactions;

namespace AssetManager.Infrastructure.Persistence.Repositories;

public sealed record DataSetSnapshot(
    ManifestDocument Manifest,
    IReadOnlyList<FieldDefinition> Fields,
    IReadOnlyList<AssetTypeDefinition> AssetTypes,
    TagCatalog Tags,
    AppSettingsDocument Settings,
    ViewSettingsDocument Views,
    RecordLoadBatchResult RecordLoadResult);

public sealed class DataSetLoader(
    AtomicFileRecoveryService atomicRecovery,
    JsonTransactionCoordinator transactions,
    DatasetMigrationService migrations,
    ManifestRepository manifests,
    FieldDefinitionRepository fields,
    AssetTypeRepository assetTypes,
    TagRepository tags,
    SettingsRepository settings,
    ViewSettingsRepository views,
    RecordRepository records)
{
    public async Task<DataSetSnapshot> LoadAsync(
        DataRootLayout layout,
        CancellationToken cancellationToken = default)
    {
        _ = await atomicRecovery.RecoverAsync(layout.RootDirectory, cancellationToken).ConfigureAwait(false);
        await transactions.RecoverPendingAsync(layout, cancellationToken).ConfigureAwait(false);
        _ = await migrations.MigrateAsync(layout, cancellationToken).ConfigureAwait(false);

        var manifest = await manifests.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var fieldDefinitions = await fields.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var typeDefinitions = await assetTypes.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var tagCatalog = await tags.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var appSettings = await settings.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var viewSettings = await views.LoadAsync(layout, cancellationToken).ConfigureAwait(false);
        var recordResult = await records.LoadAllAsync(
            layout,
            fieldDefinitions,
            cancellationToken).ConfigureAwait(false);

        return new DataSetSnapshot(
            manifest,
            fieldDefinitions,
            typeDefinitions,
            tagCatalog,
            appSettings,
            viewSettings,
            recordResult);
    }
}
