using AssetManager.Domain.Catalog;
using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.UnitTests.Domain;

public sealed class CatalogTests
{
    [Fact]
    public void AssetTypeNormalizesExtensionsAndMatchesIgnoringCase()
    {
        var type = new AssetTypeDefinition(
            new AssetTypeId("type.audio"),
            "音声",
            ["WAV", ".mp3", ".WAV"]);

        Assert.Equal([".wav", ".mp3"], type.Extensions);
        Assert.True(type.MatchesExtension(".WAV"));
    }

    [Theory]
    [InlineData("#123ABC", "#123ABC")]
    [InlineData("#ff123abc", "#FF123ABC")]
    public void TagColorAcceptsRgbAndArgb(string input, string expected)
    {
        var color = new TagColor(input);

        Assert.Equal(expected, color.Value);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    public void TagColorRejectsInvalidValue(string value)
    {
        Assert.Throws<DomainValidationException>(() => new TagColor(value));
    }
}
