using AssetManager.Application.Search;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Search;

public sealed class RecordSearchEngineTests
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
    public void TextSearchUsesNfkcOrdinalIgnoreCasePartialMatchAndTermAnd()
    {
        var record = Set(
            AssetRecord.Create(TestTime),
            BuiltInFieldIds.Name,
            new TextFieldValue("ＡＢＣ１２３ Blue Sky素材"));

        Assert.True(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.Name,
            new TextContainsCondition("abc123 sky blue"))));
        Assert.False(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.Name,
            new TextContainsCondition("abc missing"))));
    }

    [Fact]
    public void ConditionsForMultipleColumnsAreCombinedWithAnd()
    {
        var record = Set(
            AssetRecord.Create(TestTime),
            BuiltInFieldIds.Name,
            new TextFieldValue("Forest Texture"));
        record = Set(
            record,
            BuiltInFieldIds.TargetPath,
            new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\Forest\leaf.png"));
        var matching = new SearchQuery(new Dictionary<FieldId, FieldSearchCondition>
        {
            [BuiltInFieldIds.Name] = new TextContainsCondition("texture"),
            [BuiltInFieldIds.TargetPath] = new TextContainsCondition(@"forest\leaf"),
        });
        var rejected = new SearchQuery(new Dictionary<FieldId, FieldSearchCondition>
        {
            [BuiltInFieldIds.Name] = new TextContainsCondition("texture"),
            [BuiltInFieldIds.TargetPath] = new TextContainsCondition("missing"),
        });

        Assert.True(RecordSearchEngine.Matches(record, matching));
        Assert.False(RecordSearchEngine.Matches(record, rejected));
    }

    [Fact]
    public void SelectionCandidatesAreCombinedWithOr()
    {
        var record = Set(
            AssetRecord.Create(TestTime),
            BuiltInFieldIds.Tags,
            new TagSetFieldValue([new TagId("tag.fantasy"), new TagId("tag.nature")]));
        record = Set(
            record,
            BuiltInFieldIds.Status,
            new RecordStatusFieldValue(RecordStatus.Available));

        Assert.True(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.Tags,
            new OptionAnyCondition(["tag.scifi", "tag.nature"]))));
        Assert.False(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.Tags,
            new OptionAnyCondition(["tag.scifi", "tag.modern"]))));
        Assert.True(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.Status,
            new OptionAnyCondition(["available"]))));
    }

    [Fact]
    public void BooleanConditionTreatsOmittedDefaultAsFalse()
    {
        var omitted = AssetRecord.Create(TestTime);
        var enabled = Set(
            AssetRecord.Create(TestTime),
            BuiltInFieldIds.Favorite,
            new BooleanFieldValue(true));

        Assert.True(RecordSearchEngine.Matches(omitted, Query(
            BuiltInFieldIds.Favorite,
            new BooleanEqualsCondition(false))));
        Assert.True(RecordSearchEngine.Matches(enabled, Query(
            BuiltInFieldIds.Favorite,
            new BooleanEqualsCondition(true))));
        Assert.False(RecordSearchEngine.Matches(omitted, Query(
            BuiltInFieldIds.Favorite,
            new BooleanEqualsCondition(true))));
    }

    [Fact]
    public void CustomFieldsAndDedicatedListsAreSearchable()
    {
        var custom = FieldDefinition.CreateCustom(CustomFieldId.New(), "自由欄", FieldType.MultilineText);
        var record = AssetRecord.Create(TestTime).SetValue(
            custom,
            new MultilineTextFieldValue("個別の検索対象"),
            TestTime);
        record = Set(
            record,
            BuiltInFieldIds.RelatedDocuments,
            new RelatedDocumentListFieldValue(
            [
                new RelatedDocument("利用規約", @"C:\Docs\terms.pdf"),
            ]));

        Assert.True(RecordSearchEngine.Matches(record, Query(
            custom.Id,
            new TextContainsCondition("検索"))));
        Assert.True(RecordSearchEngine.Matches(record, Query(
            BuiltInFieldIds.RelatedDocuments,
            new TextContainsCondition("利用 terms"))));
    }

    private static SearchQuery Query(FieldId id, FieldSearchCondition condition)
    {
        return new SearchQuery(new Dictionary<FieldId, FieldSearchCondition> { [id] = condition });
    }

    private static AssetRecord Set(AssetRecord record, FieldId id, FieldValue value)
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == id);
        return record.SetValue(definition, value, TestTime);
    }
}
