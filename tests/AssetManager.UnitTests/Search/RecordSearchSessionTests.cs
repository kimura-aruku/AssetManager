using AssetManager.Application.Search;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Search;

public sealed class RecordSearchSessionTests
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
    public void SearchUsesAllRecordsAndDisplaysOneHundredAtATime()
    {
        var nameDefinition = BuiltInFieldCatalog.All.Single(field => field.Id == BuiltInFieldIds.Name);
        var records = Enumerable.Range(0, 250).Select(index =>
            AssetRecord.Create(TestTime).SetValue(
                nameDefinition,
                new TextFieldValue($"match-{index:D3}"),
                TestTime));
        var session = new RecordSearchSession(
            records,
            new SearchQuery(new Dictionary<FieldId, FieldSearchCondition>
            {
                [BuiltInFieldIds.Name] = new TextContainsCondition("match"),
            }));

        Assert.Equal(250, session.TotalCount);
        Assert.Equal(100, session.VisibleRecords.Count);
        Assert.True(session.HasMore);
        Assert.Equal(100, session.LoadMore().Count);
        Assert.Equal(200, session.VisibleRecords.Count);
        Assert.Equal(50, session.LoadMore().Count);
        Assert.Equal(250, session.VisibleRecords.Count);
        Assert.False(session.HasMore);
        Assert.Empty(session.LoadMore());
    }
}
