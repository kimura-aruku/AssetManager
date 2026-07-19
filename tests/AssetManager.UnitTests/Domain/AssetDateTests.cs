using AssetManager.Domain.Common;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Domain;

public sealed class AssetDateTests
{
    [Fact]
    public void ConvertsBetweenStorageAndDisplayFormats()
    {
        var value = AssetDate.ParseStorage("2026-07-19");

        Assert.Equal("2026/07/19", value.ToDisplayString());
        Assert.Equal(value, AssetDate.ParseDisplay("2026/07/19"));
    }

    [Theory]
    [InlineData("2026/07/19")]
    [InlineData("2026-02-30")]
    [InlineData("")]
    public void StorageParserRejectsInvalidDate(string value)
    {
        Assert.Throws<DomainValidationException>(() => AssetDate.ParseStorage(value));
    }
}
