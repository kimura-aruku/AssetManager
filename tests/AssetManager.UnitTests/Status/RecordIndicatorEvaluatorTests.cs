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
            CommercialUseAllowed: true,
            ModificationAllowed: true,
            ProductEmbeddingAllowed: true,
            CreditDisplayRequired: true,
            GenerativeAiInputAllowed: true));

        Assert.Equal(
            [
                LicenseBadgeKind.CommercialUseAllowed,
                LicenseBadgeKind.ModificationAllowed,
                LicenseBadgeKind.ProductEmbeddingAllowed,
                LicenseBadgeKind.CreditDisplayRequired,
                LicenseBadgeKind.GenerativeAiInputAllowed,
            ],
            badges.Select(badge => badge.Kind));
        Assert.False(badges[0].IsRequired);
        Assert.False(badges[1].IsRequired);
        Assert.False(badges[2].IsRequired);
        Assert.True(badges[3].IsRequired);
        Assert.False(badges[4].IsRequired);
        Assert.Equal(
            "クレジット画面など、利用者が確認できる場所への作者名等の表示が必要です。",
            badges[3].ToolTip);
        Assert.DoesNotContain("利用者向けの作者表示が必要", badges[3].ToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluateはライセンスと対象パス未入力を重大度固定順で返す()
    {
        var record = SetValue(
            SetValue(
                AssetRecord.Create(Now),
                BuiltInFieldIds.LicenseReviewRequired,
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

    [Fact]
    public void ライセンス条件定義は指定された12項目と説明を保持する()
    {
        Assert.Equal(12, LicenseConditionCatalog.All.Count);
        Assert.Equal(
            [
                ("商用利用可", "商用利用できる", "営利目的の製品・サービスで利用できます。"),
                ("改変可", "内容を変更できる", "元データやソースコードを編集・加工できます。"),
                ("製品組込み可", "製品に組み込める", "ゲームやアプリなどの製品に組み込んで利用・配布できます。"),
                ("元データ再配布可", "元データを再配布できる", "元データを単体または取り出せる形で第三者に配布できます。"),
                ("クレジット表示必須", "利用者向けの作者表示が必要", "クレジット画面など、利用者が確認できる場所への作者名等の表示が必要です。"),
                ("著作権表示保持必須", "Copyright表記を残す必要がある", "元データに含まれる著作権表示を削除せず保持する必要があります。"),
                ("ライセンス文添付必須", "ライセンス本文の添付が必要", "配布物にライセンス本文または指定された許諾表示を含める必要があります。"),
                ("同一ライセンス継承必須", "派生物にも同じライセンスを適用する必要がある", "改変物や派生物を配布する場合、同一または指定されたライセンスの適用が必要です。"),
                ("AI学習利用可", "AIモデルの学習に利用できる", "AI・機械学習モデルの学習データとして利用できます。"),
                ("生成AI入力可", "生成AIへ入力できる", "生成AIのプロンプト、参照画像、入力データなどとして利用できます。"),
                ("エンジン制限あり", "特定のエンジンでのみ利用できる", "Unity限定など、利用できるゲームエンジンや環境に制限があります。"),
                ("再確認必要", "利用前の再確認が必要", "規約変更、個別条件、用途制限などを利用前に再確認する必要があります。"),
            ],
            LicenseConditionCatalog.All.Select(condition =>
                (condition.Label, condition.Summary, condition.Description)));
    }

    private static AssetRecord SetValue(AssetRecord record, FieldId id, FieldValue value)
    {
        var definition = BuiltInFieldCatalog.All.Single(field => field.Id == id);
        return record.SetValue(definition, value, record.UpdatedAt);
    }
}
