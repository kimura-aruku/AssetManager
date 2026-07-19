using AssetManager.App.Presentation;
using AssetManager.Application.Status;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.IntegrationTests.Presentation;

public sealed class RecordRowViewModelTests
{
    [Fact]
    public void RowExposesFavoriteAndLicenseExpiryForFixedColumns()
    {
        var now = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var expiry = new AssetDate(new DateOnly(2027, 3, 31));
        var favoriteDefinition = GetDefinition(BuiltInFieldIds.Favorite);
        var expiryDefinition = GetDefinition(BuiltInFieldIds.LicenseExpiryDate);
        var record = AssetRecord.Create(now)
            .SetValue(favoriteDefinition, new BooleanFieldValue(true), now)
            .SetValue(expiryDefinition, new DateFieldValue(expiry), now);
        var definitions = BuiltInFieldCatalog.All.ToDictionary(definition => definition.Id);

        var row = new RecordRowViewModel(
            record,
            new Dictionary<AssetManager.Domain.Identifiers.AssetTypeId, string>(),
            new Dictionary<AssetManager.Domain.Identifiers.TagId, string>(),
            definitions,
            new AssetDate(new DateOnly(2026, 7, 20)),
            new LicenseWarningPolicy(365),
            new Dictionary<string, AssetManager.Application.Paths.PathCheckResult>());

        Assert.Equal("★", row.FavoriteGlyph);
        Assert.Equal(expiry.ToDisplayString(), row.LicenseExpiryDate);
    }

    private static FieldDefinition GetDefinition(AssetManager.Domain.Identifiers.FieldId id)
    {
        return BuiltInFieldCatalog.All.Single(definition => definition.Id == id);
    }
}
