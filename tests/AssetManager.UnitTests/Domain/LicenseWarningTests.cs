using AssetManager.Domain.Licensing;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Domain;

public sealed class LicenseWarningTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EvaluatesAllApplicableLicenseWarnings()
    {
        var review = new LicenseReviewInfo(
            new LicenseTerms(NeedsReview: true),
            AssetDate.ParseStorage("2025-07-18"),
            AssetDate.ParseStorage("2026-07-18"));

        var warnings = LicenseWarningEvaluator.Evaluate(
            review,
            AssetDate.ParseStorage("2026-07-19"));

        Assert.Equal(
            [
                LicenseWarningKind.Expired,
                LicenseWarningKind.ReviewOverdue,
                LicenseWarningKind.NeedsReview,
            ],
            warnings.Select(warning => warning.Kind));
        Assert.Equal(LicenseWarningSeverity.Error, warnings[0].Severity);
    }

    [Fact]
    public void ReviewWarningDoesNotAppearOnExactBoundary()
    {
        var review = new LicenseReviewInfo(
            new LicenseTerms(),
            AssetDate.ParseStorage("2025-07-19"));

        var warnings = LicenseWarningEvaluator.Evaluate(
            review,
            AssetDate.ParseStorage("2026-07-19"));

        Assert.DoesNotContain(warnings, warning => warning.Kind == LicenseWarningKind.ReviewOverdue);
    }

    [Fact]
    public void CreatesLicenseReviewFromRecordValues()
    {
        var reviewDefinition = GetDefinition(BuiltInFieldIds.LicenseReviewRequired);
        var checkedDefinition = GetDefinition(BuiltInFieldIds.LicenseLastCheckedDate);
        var record = AssetRecord.Create(Now)
            .SetValue(reviewDefinition, new BooleanFieldValue(true), Now.AddMinutes(1))
            .SetValue(
                checkedDefinition,
                new DateFieldValue(AssetDate.ParseStorage("2026-07-19")),
                Now.AddMinutes(2));

        var review = LicenseReviewInfo.FromRecord(record);

        Assert.True(review.Terms.NeedsReview);
        Assert.Equal("2026-07-19", review.LastCheckedDate?.ToStorageString());
    }

    private static FieldDefinition GetDefinition(FieldId id)
    {
        return BuiltInFieldCatalog.All.Single(definition => definition.Id == id);
    }
}
