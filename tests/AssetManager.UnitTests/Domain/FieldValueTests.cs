using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Domain;

public sealed class FieldValueTests
{
    [Theory]
    [InlineData("https://example.invalid/product")]
    [InlineData("http://example.invalid/license")]
    public void UrlValueAcceptsWebUrl(string value)
    {
        var url = new UrlFieldValue(value);

        Assert.Equal(value, url.Value);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("file:///C:/license.txt")]
    [InlineData("")]
    public void UrlValueRejectsUnsupportedUrl(string value)
    {
        Assert.Throws<DomainValidationException>(() => new UrlFieldValue(value));
    }

    [Fact]
    public void MultiValueRejectsDuplicateReferences()
    {
        var id = new AssetTypeId("type.bgm");

        Assert.Throws<DomainValidationException>(() => new AssetTypeSetFieldValue([id, id]));
    }

    [Fact]
    public void DedicatedListsKeepMultipleValues()
    {
        var creators = new CreatorListFieldValue(["制作者A", "制作者B"]);
        var documents = new RelatedDocumentListFieldValue(
            [new RelatedDocument("利用規約", "C:\\Assets\\LICENSE.pdf")]);
        var urls = new RelatedUrlListFieldValue(
            [new RelatedUrl("商品ページ", "https://example.invalid/product")]);

        Assert.Equal(2, creators.Values.Count);
        Assert.Single(documents.Values);
        Assert.Single(urls.Values);
    }
}
