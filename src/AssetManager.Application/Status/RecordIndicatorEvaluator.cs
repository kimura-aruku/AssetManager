using AssetManager.Application.Paths;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Status;

public enum LicenseBadgeKind
{
    CommercialUseAllowed,
    ModificationAllowed,
    ProductEmbeddingAllowed,
    OriginalDataRedistributionAllowed,
    CreditDisplayRequired,
    CopyrightNoticeRetentionRequired,
    LicenseTextAttachmentRequired,
    SameLicenseRequired,
    AiTrainingAllowed,
    GenerativeAiInputAllowed,
    EngineRestrictionExists,
    LicenseReviewRequired,
}

public sealed record LicenseBadge(
    LicenseBadgeKind Kind,
    string Glyph,
    string Name,
    string Summary,
    string Description,
    bool IsRequired)
{
    public string ToolTip => $"{Name}{Environment.NewLine}{Summary}{Environment.NewLine}{Description}";
}

public enum RecordIndicatorKind
{
    LicenseExpired,
    PathMissing,
    PathAccessDenied,
    PathError,
    TargetPathMissing,
    LicenseReviewOverdue,
    LicenseNeedsReview,
    PathChecking,
    PathUnchecked,
    PathsAvailable,
}

public enum RecordIndicatorSeverity
{
    Error,
    Warning,
    Information,
    Success,
}

public sealed record RecordIndicator(
    RecordIndicatorKind Kind,
    RecordIndicatorSeverity Severity,
    string Glyph,
    string Name,
    string Reason)
{
    public string ToolTip => $"{Name}: {Reason}";
}

public static class LicenseBadgeEvaluator
{
    public static IReadOnlyList<LicenseBadge> Evaluate(LicenseTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var badges = new List<LicenseBadge>();
        foreach (var condition in LicenseConditionCatalog.All)
        {
            if (!terms.GetValue(condition.SystemRole))
            {
                continue;
            }

            badges.Add(new LicenseBadge(
                MapKind(condition.SystemRole),
                condition.Glyph,
                condition.Label,
                condition.Summary,
                condition.Description,
                condition.IsRequired));
        }

        return badges;

        static LicenseBadgeKind MapKind(SystemRole role)
        {
            return role switch
            {
                SystemRole.CommercialUseAllowed => LicenseBadgeKind.CommercialUseAllowed,
                SystemRole.ModificationAllowed => LicenseBadgeKind.ModificationAllowed,
                SystemRole.ProductEmbeddingAllowed => LicenseBadgeKind.ProductEmbeddingAllowed,
                SystemRole.OriginalDataRedistributionAllowed => LicenseBadgeKind.OriginalDataRedistributionAllowed,
                SystemRole.CreditDisplayRequired => LicenseBadgeKind.CreditDisplayRequired,
                SystemRole.CopyrightNoticeRetentionRequired => LicenseBadgeKind.CopyrightNoticeRetentionRequired,
                SystemRole.LicenseTextAttachmentRequired => LicenseBadgeKind.LicenseTextAttachmentRequired,
                SystemRole.SameLicenseRequired => LicenseBadgeKind.SameLicenseRequired,
                SystemRole.AiTrainingAllowed => LicenseBadgeKind.AiTrainingAllowed,
                SystemRole.GenerativeAiInputAllowed => LicenseBadgeKind.GenerativeAiInputAllowed,
                SystemRole.EngineRestrictionExists => LicenseBadgeKind.EngineRestrictionExists,
                SystemRole.LicenseReviewRequired => LicenseBadgeKind.LicenseReviewRequired,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
            };
        }
    }
}

public static class RecordIndicatorEvaluator
{
    public static IReadOnlyList<RecordIndicator> Evaluate(
        AssetRecord record,
        AssetDate today,
        LicenseWarningPolicy policy,
        IReadOnlyDictionary<string, PathCheckResult> pathResults)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(pathResults);
        var indicators = new List<RecordIndicator>();

        foreach (var warning in LicenseWarningEvaluator.Evaluate(
                     LicenseReviewInfo.FromRecord(record),
                     today,
                     policy))
        {
            indicators.Add(MapWarning(warning));
        }

        if (record.TargetPath is null)
        {
            indicators.Add(new RecordIndicator(
                RecordIndicatorKind.TargetPathMissing,
                RecordIndicatorSeverity.Error,
                "!",
                "パス未入力",
                "管理対象のファイルまたはフォルダーが指定されていません。"));
        }

        var paths = PathReferenceExtractor.Extract([record]);
        var pathStates = paths.Select(path => pathResults.TryGetValue(path, out var result)
            ? result
            : new PathCheckResult(path, PathCheckStatus.Unchecked)).ToArray();
        foreach (var result in pathStates.Where(result => result.Status is not PathCheckStatus.Available))
        {
            indicators.Add(MapPathResult(result));
        }

        if (pathStates.Length > 0 && pathStates.All(result => result.Status == PathCheckStatus.Available))
        {
            indicators.Add(new RecordIndicator(
                RecordIndicatorKind.PathsAvailable,
                RecordIndicatorSeverity.Success,
                "✓",
                "パス確認済み",
                $"関連する{pathStates.Length}件のパスがすべて存在します。"));
        }

        return indicators
            .OrderBy(indicator => indicator.Severity)
            .ThenBy(indicator => indicator.Kind)
            .ThenBy(indicator => indicator.Reason, StringComparer.Ordinal)
            .ToArray();
    }

    private static RecordIndicator MapWarning(LicenseWarning warning)
    {
        return warning.Kind switch
        {
            LicenseWarningKind.Expired => Create(
                RecordIndicatorKind.LicenseExpired,
                RecordIndicatorSeverity.Error,
                "×",
                "ライセンス期限切れ"),
            LicenseWarningKind.ReviewOverdue => Create(
                RecordIndicatorKind.LicenseReviewOverdue,
                RecordIndicatorSeverity.Warning,
                "⏱",
                "最終確認日超過"),
            LicenseWarningKind.NeedsReview => Create(
                RecordIndicatorKind.LicenseNeedsReview,
                RecordIndicatorSeverity.Warning,
                "!",
                "再確認必要"),
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        };

        RecordIndicator Create(
            RecordIndicatorKind kind,
            RecordIndicatorSeverity severity,
            string glyph,
            string name)
        {
            return new RecordIndicator(kind, severity, glyph, name, warning.Reason);
        }
    }

    private static RecordIndicator MapPathResult(PathCheckResult result)
    {
        var path = result.Path;
        return result.Status switch
        {
            PathCheckStatus.Missing => Create(
                RecordIndicatorKind.PathMissing,
                RecordIndicatorSeverity.Error,
                "×",
                "パス不存在",
                $"パスが存在しません: {path}"),
            PathCheckStatus.AccessDenied => Create(
                RecordIndicatorKind.PathAccessDenied,
                RecordIndicatorSeverity.Error,
                "鍵",
                "アクセス拒否",
                $"パスへアクセスする権限がありません: {path}"),
            PathCheckStatus.Error => Create(
                RecordIndicatorKind.PathError,
                RecordIndicatorSeverity.Error,
                "!",
                "パス確認エラー",
                $"パスを確認できませんでした: {path}{FormatError(result.Error)}"),
            PathCheckStatus.Checking => Create(
                RecordIndicatorKind.PathChecking,
                RecordIndicatorSeverity.Information,
                "…",
                "パス確認中",
                $"パスを確認しています: {path}"),
            PathCheckStatus.Unchecked => Create(
                RecordIndicatorKind.PathUnchecked,
                RecordIndicatorSeverity.Information,
                "○",
                "パス未確認",
                $"パスはまだ確認されていません: {path}"),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

        static RecordIndicator Create(
            RecordIndicatorKind kind,
            RecordIndicatorSeverity severity,
            string glyph,
            string name,
            string reason)
        {
            return new RecordIndicator(kind, severity, glyph, name, reason);
        }
    }

    private static string FormatError(string? error)
    {
        return string.IsNullOrWhiteSpace(error) ? string.Empty : $"（{error}）";
    }
}
