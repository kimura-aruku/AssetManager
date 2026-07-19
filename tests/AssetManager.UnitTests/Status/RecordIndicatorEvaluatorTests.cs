using AssetManager.Application.Paths;
using AssetManager.Application.Status;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Status;

public sealed class RecordIndicatorEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LicenseBadgeEvaluatorは必須項目と許可項目を固定順で返す()
    {
        var badges = LicenseBadgeEvaluator.Evaluate(new LicenseTerms(
            CreditRequired: true,
            LinkRequired: true,
            CommercialUseAllowed: true,
            ModificationAllowed: true,
            GenerativeAiUseAllowed: true));

        Assert.Equal(
            [
                LicenseBadgeKind.CreditRequired,
                LicenseBadgeKind.LinkRequired,
                LicenseBadgeKind.CommercialUseAllowed,
                LicenseBadgeKind.ModificationAllowed,
                LicenseBadgeKind.GenerativeAiUseAllowed,
            ],
            badges.Select(badge => badge.Kind));
        Assert.All(badges.Take(2), badge => Assert.True(badge.IsRequired));
        Assert.All(badges.Skip(2), badge => Assert.False(badge.IsRequired));
    }

    [Fact]
    public void Evaluateはライセンスと対象パス未入力を重大度固定順で返す()
    {
        var record = SetValue(
            SetValue(
                SetValue(AssetRecord.Create(Now), BuiltInFieldIds.LicenseUnknown, new BooleanFieldValue(true)),
                BuiltInFieldIds.LicenseNeedsReview,
                new BooleanFieldValue(true)),
            BuiltInFieldIds.LicenseExpiryDate,
            new DateFieldValue(AssetDate.ParseDisplay("2026/07/18")));

        var indicators = RecordIndicatorEvaluator.Evaluate(
            record,
            AssetDate.ParseDisplay("2026/07/19"),
            new LicenseWarningPolicy(),
            new Dictionary<string, PathCheckResult>());

        Assert.Equal(
            [
                RecordIndicatorKind.LicenseExpired,
                RecordIndicatorKind.TargetPathMissing,
                RecordIndicatorKind.LicenseConditionsUnknown,
                RecordIndicatorKind.LicenseNeedsReview,
            ],
            indicators.Select(indicator => indicator.Kind));
        Assert.All(indicators.Take(2), indicator => Assert.Equal(RecordIndicatorSeverity.Error, indicator.Severity));
    }

    [Theory]
    [InlineData(PathCheckStatus.Missing, RecordIndicatorKind.PathMissing, RecordIndicatorSeverity.Error)]
    [InlineData(PathCheckStatus.AccessDenied, RecordIndicatorKind.PathAccessDenied, RecordIndicatorSeverity.Error)]
    [InlineData(PathCheckStatus.Error, RecordIndicatorKind.PathError, RecordIndicatorSeverity.Error)]
    [InlineData(PathCheckStatus.Checking, RecordIndicatorKind.PathChecking, RecordIndicatorSeverity.Information)]
    [InlineData(PathCheckStatus.Unchecked, RecordIndicatorKind.PathUnchecked, RecordIndicatorSeverity.Information)]
    public void Evaluateは各パス確認結果を状態へ変換する(
        PathCheckStatus status,
        RecordIndicatorKind expectedKind,
        RecordIndicatorSeverity expectedSeverity)
    {
        const string path = @"C:\Assets\item.png";
        var record = SetValue(
            AssetRecord.Create(Now),
            BuiltInFieldIds.TargetPath,
            new TargetPathFieldValue(TargetPathKind.File, path));
        var cache = new Dictionary<string, PathCheckResult>(StringComparer.OrdinalIgnoreCase)
        {
            [path] = new PathCheckResult(path, status, status == PathCheckStatus.Error ? "I/Oエラー" : null),
        };

        var indicator = Assert.Single(RecordIndicatorEvaluator.Evaluate(
            record,
            AssetDate.ParseDisplay("2026/07/19"),
            new LicenseWarningPolicy(),
            cache));

        Assert.Equal(expectedKind, indicator.Kind);
        Assert.Equal(expectedSeverity, indicator.Severity);
        Assert.Contains(path, indicator.ToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluateは全パス利用可能を成功状態にする()
    {
        const string path = @"C:\Assets\item.png";
        var record = SetValue(
            AssetRecord.Create(Now),
            BuiltInFieldIds.TargetPath,
            new TargetPathFieldValue(TargetPathKind.File, path));
        var cache = new Dictionary<string, PathCheckResult>(StringComparer.OrdinalIgnoreCase)
        {
            [path] = new PathCheckResult(path, PathCheckStatus.Available),
        };

        var indicator = Assert.Single(RecordIndicatorEvaluator.Evaluate(
            record,
            AssetDate.ParseDisplay("2026/07/19"),
            new LicenseWarningPolicy(),
            cache));

        Assert.Equal(RecordIndicatorKind.PathsAvailable, indicator.Kind);
        Assert.Equal(RecordIndicatorSeverity.Success, indicator.Severity);
    }

    private static AssetRecord SetValue(AssetRecord record, FieldId id, FieldValue value)
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == id);
        return record.SetValue(definition, value, record.UpdatedAt);
    }
}
