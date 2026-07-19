using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Domain;

public sealed class AssetRecordTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetValueReturnsUpdatedRecordWithoutChangingOriginal()
    {
        var record = AssetRecord.Create(CreatedAt);
        var statusDefinition = GetDefinition(BuiltInFieldIds.Status);
        var favoriteDefinition = GetDefinition(BuiltInFieldIds.Favorite);

        var updated = record
            .SetValue(statusDefinition, new RecordStatusFieldValue(RecordStatus.Available), CreatedAt.AddMinutes(1))
            .SetValue(favoriteDefinition, new BooleanFieldValue(true), CreatedAt.AddMinutes(2));

        Assert.Equal(RecordStatus.Unchecked, record.Status);
        Assert.False(record.Favorite.IsFavorite);
        Assert.Equal(RecordStatus.Available, updated.Status);
        Assert.True(updated.Favorite.IsFavorite);
        Assert.Equal(record.Id, updated.Id);
        Assert.Equal(CreatedAt.AddMinutes(2), updated.UpdatedAt);
    }

    [Fact]
    public void SetValueRejectsMismatchedType()
    {
        var record = AssetRecord.Create(CreatedAt);
        var nameDefinition = GetDefinition(BuiltInFieldIds.Name);

        Assert.Throws<DomainValidationException>(() => record.SetValue(
            nameDefinition,
            new NumberFieldValue(10),
            CreatedAt.AddMinutes(1)));
    }

    [Fact]
    public void RecordRejectsNonUtcTimestamp()
    {
        var localTime = new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.FromHours(9));

        Assert.Throws<DomainValidationException>(() => AssetRecord.Create(localTime));
    }

    private static FieldDefinition GetDefinition(FieldId id)
    {
        return BuiltInFieldCatalog.All.Single(definition => definition.Id == id);
    }
}
