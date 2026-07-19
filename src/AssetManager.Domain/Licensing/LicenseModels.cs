using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Domain.Licensing;

public sealed record LicenseTerms(
    bool CreditRequired = false,
    bool LinkRequired = false,
    bool LogoRequired = false,
    bool CommercialUseAllowed = false,
    bool ModificationAllowed = false,
    bool RedistributionAllowed = false,
    bool AdultUseAllowed = false,
    bool GenerativeAiUseAllowed = false,
    bool ConditionsUnknown = false,
    bool NeedsReview = false)
{
    public static LicenseTerms FromRecord(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new LicenseTerms(
            CreditRequired: ReadBoolean(record, BuiltInFieldIds.CreditRequired),
            LinkRequired: ReadBoolean(record, BuiltInFieldIds.LinkRequired),
            LogoRequired: ReadBoolean(record, BuiltInFieldIds.LogoRequired),
            CommercialUseAllowed: ReadBoolean(record, BuiltInFieldIds.CommercialUseAllowed),
            ModificationAllowed: ReadBoolean(record, BuiltInFieldIds.ModificationAllowed),
            RedistributionAllowed: ReadBoolean(record, BuiltInFieldIds.RedistributionAllowed),
            AdultUseAllowed: ReadBoolean(record, BuiltInFieldIds.AdultUseAllowed),
            GenerativeAiUseAllowed: ReadBoolean(record, BuiltInFieldIds.GenerativeAiUseAllowed),
            ConditionsUnknown: ReadBoolean(record, BuiltInFieldIds.LicenseUnknown),
            NeedsReview: ReadBoolean(record, BuiltInFieldIds.LicenseNeedsReview));
    }

    private static bool ReadBoolean(AssetRecord record, FieldId fieldId)
    {
        return record.GetValue<BooleanFieldValue>(fieldId)?.Value ?? false;
    }
}

public sealed record LicenseReviewInfo(
    LicenseTerms Terms,
    AssetDate? LastCheckedDate = null,
    AssetDate? ExpiryDate = null)
{
    public static LicenseReviewInfo FromRecord(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new LicenseReviewInfo(
            LicenseTerms.FromRecord(record),
            record.GetValue<DateFieldValue>(BuiltInFieldIds.LicenseLastCheckedDate)?.Value,
            record.GetValue<DateFieldValue>(BuiltInFieldIds.LicenseExpiryDate)?.Value);
    }
}

public sealed record LicenseWarningPolicy
{
    public const int DefaultReviewWarningDays = 365;

    public LicenseWarningPolicy(int reviewWarningDays = DefaultReviewWarningDays)
    {
        if (reviewWarningDays < 1)
        {
            throw new DomainValidationException("警告日数は1日以上にしてください。", nameof(reviewWarningDays));
        }

        ReviewWarningDays = reviewWarningDays;
    }

    public int ReviewWarningDays { get; }
}

public enum LicenseWarningKind
{
    Expired,
    ReviewOverdue,
    ConditionsUnknown,
    NeedsReview,
}

public enum LicenseWarningSeverity
{
    Warning,
    Error,
}

public sealed record LicenseWarning(
    LicenseWarningKind Kind,
    LicenseWarningSeverity Severity,
    string Reason);

public static class LicenseWarningEvaluator
{
    public static IReadOnlyList<LicenseWarning> Evaluate(
        LicenseReviewInfo review,
        AssetDate today,
        LicenseWarningPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(review);
        policy ??= new LicenseWarningPolicy();
        var warnings = new List<LicenseWarning>();

        if (review.ExpiryDate is { } expiry && today.Value > expiry.Value)
        {
            warnings.Add(new LicenseWarning(
                LicenseWarningKind.Expired,
                LicenseWarningSeverity.Error,
                $"ライセンス期限（{expiry.ToDisplayString()}）を過ぎています。"));
        }

        if (review.LastCheckedDate is { } lastChecked
            && today.Value > lastChecked.Value.AddDays(policy.ReviewWarningDays))
        {
            warnings.Add(new LicenseWarning(
                LicenseWarningKind.ReviewOverdue,
                LicenseWarningSeverity.Warning,
                $"ライセンスの最終確認から{policy.ReviewWarningDays}日を超えています。"));
        }

        if (review.Terms.ConditionsUnknown)
        {
            warnings.Add(new LicenseWarning(
                LicenseWarningKind.ConditionsUnknown,
                LicenseWarningSeverity.Warning,
                "ライセンス条件が不明です。"));
        }

        if (review.Terms.NeedsReview)
        {
            warnings.Add(new LicenseWarning(
                LicenseWarningKind.NeedsReview,
                LicenseWarningSeverity.Warning,
                "ライセンス条件の再確認が必要です。"));
        }

        return warnings.AsReadOnly();
    }
}
