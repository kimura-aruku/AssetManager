using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.UnitTests.Domain;

public sealed class IdentifierTests
{
    [Fact]
    public void NewRecordIdCreatesUuidVersion7()
    {
        var id = RecordId.New();

        Assert.Equal('7', id.ToString()[14]);
    }

    [Fact]
    public void RecordIdRejectsNonVersion7Uuid()
    {
        Assert.Throws<DomainValidationException>(() => new RecordId(Guid.NewGuid()));
    }

    [Fact]
    public void CustomFieldIdRoundTripsExternalFormat()
    {
        var id = CustomFieldId.New();

        var parsed = CustomFieldId.Parse(id.ToString());

        Assert.Equal(id, parsed);
        Assert.StartsWith(CustomFieldId.Prefix, id.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("builtin.name", true, false)]
    [InlineData("custom.019f0000-0000-7000-8000-000000000001", false, true)]
    public void FieldIdRecognizesOrigin(string value, bool isBuiltIn, bool isCustom)
    {
        var id = new FieldId(value);

        Assert.Equal(isBuiltIn, id.IsBuiltIn);
        Assert.Equal(isCustom, id.IsCustom);
    }
}
