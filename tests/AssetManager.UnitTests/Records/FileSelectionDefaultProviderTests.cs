using AssetManager.Application.Records;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Identifiers;

namespace AssetManager.UnitTests.Records;

public sealed class FileSelectionDefaultProviderTests
{
    [Fact]
    public void Createは拡張子を除いたファイル名と一致する種類を返す()
    {
        var image = new AssetTypeDefinition(new AssetTypeId("type.image"), "画像", [".png", ".jpg"]);
        var ui = new AssetTypeDefinition(new AssetTypeId("type.ui"), "UI", [".png"]);

        var result = FileSelectionDefaultProvider.Create(
            @"C:\素材\buttons.title.png",
            [image, ui]);

        Assert.Equal("buttons.title", result.SuggestedName);
        Assert.Equal([image.Id, ui.Id], result.SuggestedTypeIds);
    }

    [Fact]
    public void Createは未知の拡張子では種類を提案しない()
    {
        var image = new AssetTypeDefinition(new AssetTypeId("type.image"), "画像", [".png"]);

        var result = FileSelectionDefaultProvider.Create(@"D:\素材\readme.xyz", [image]);

        Assert.Equal("readme", result.SuggestedName);
        Assert.Empty(result.SuggestedTypeIds);
    }
}
