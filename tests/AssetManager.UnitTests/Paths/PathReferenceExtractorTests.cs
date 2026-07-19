using AssetManager.Application.Paths;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Paths;

public sealed class PathReferenceExtractorTests
{
    private static readonly DateTimeOffset TestTime = new(
        2026,
        7,
        19,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public void ExtractIncludesTargetAuxiliaryAndRelatedDocumentPathsOnce()
    {
        var record = AssetRecord.Create(TestTime);
        record = Set(record, BuiltInFieldIds.TargetPath, new TargetPathFieldValue(
            TargetPathKind.File,
            @"C:\Assets\main.png"));
        record = Set(record, BuiltInFieldIds.LicenseFilePath, new FilePathFieldValue(
            @"C:\Assets\license.txt"));
        record = Set(record, BuiltInFieldIds.RelatedDocuments, new RelatedDocumentListFieldValue(
        [
            new RelatedDocument("同一", @"c:/assets/LICENSE.txt"),
            new RelatedDocument("別文書", @"C:\Assets\readme.txt"),
        ]));

        var paths = PathReferenceExtractor.Extract([record]);

        Assert.Equal(3, paths.Count);
        Assert.Contains(@"C:\Assets\main.png", paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(@"C:\Assets\license.txt", paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(@"C:\Assets\readme.txt", paths, StringComparer.OrdinalIgnoreCase);
    }

    private static AssetRecord Set(AssetRecord record, FieldId id, FieldValue value)
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == id);
        return record.SetValue(definition, value, TestTime);
    }
}
