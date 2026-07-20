using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Domain.Licensing;

public sealed record LicenseTerms(
    bool CommercialUseAllowed = false,
    bool ModificationAllowed = false,
    bool ProductEmbeddingAllowed = false,
    bool OriginalDataRedistributionAllowed = false,
    bool CreditDisplayRequired = false,
    bool CopyrightNoticeRetentionRequired = false,
    bool LicenseTextAttachmentRequired = false,
    bool SameLicenseRequired = false,
    bool AiTrainingAllowed = false,
    bool GenerativeAiInputAllowed = false,
    bool EngineRestrictionExists = false,
    bool NeedsReview = false)
{
    public static LicenseTerms FromRecord(AssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new LicenseTerms(
            CommercialUseAllowed: ReadBoolean(record, BuiltInFieldIds.CommercialUseAllowed),
            ModificationAllowed: ReadBoolean(record, BuiltInFieldIds.ModificationAllowed),
            ProductEmbeddingAllowed: ReadBoolean(record, BuiltInFieldIds.ProductEmbeddingAllowed),
            OriginalDataRedistributionAllowed: ReadBoolean(record, BuiltInFieldIds.OriginalDataRedistributionAllowed),
            CreditDisplayRequired: ReadBoolean(record, BuiltInFieldIds.CreditDisplayRequired),
            CopyrightNoticeRetentionRequired: ReadBoolean(record, BuiltInFieldIds.CopyrightNoticeRetentionRequired),
            LicenseTextAttachmentRequired: ReadBoolean(record, BuiltInFieldIds.LicenseTextAttachmentRequired),
            SameLicenseRequired: ReadBoolean(record, BuiltInFieldIds.SameLicenseRequired),
            AiTrainingAllowed: ReadBoolean(record, BuiltInFieldIds.AiTrainingAllowed),
            GenerativeAiInputAllowed: ReadBoolean(record, BuiltInFieldIds.GenerativeAiInputAllowed),
            EngineRestrictionExists: ReadBoolean(record, BuiltInFieldIds.EngineRestrictionExists),
            NeedsReview: ReadBoolean(record, BuiltInFieldIds.LicenseReviewRequired));
    }

    public bool GetValue(SystemRole? role)
    {
        return role switch
        {
            SystemRole.CommercialUseAllowed => CommercialUseAllowed,
            SystemRole.ModificationAllowed => ModificationAllowed,
            SystemRole.ProductEmbeddingAllowed => ProductEmbeddingAllowed,
            SystemRole.OriginalDataRedistributionAllowed => OriginalDataRedistributionAllowed,
            SystemRole.CreditDisplayRequired => CreditDisplayRequired,
            SystemRole.CopyrightNoticeRetentionRequired => CopyrightNoticeRetentionRequired,
            SystemRole.LicenseTextAttachmentRequired => LicenseTextAttachmentRequired,
            SystemRole.SameLicenseRequired => SameLicenseRequired,
            SystemRole.AiTrainingAllowed => AiTrainingAllowed,
            SystemRole.GenerativeAiInputAllowed => GenerativeAiInputAllowed,
            SystemRole.EngineRestrictionExists => EngineRestrictionExists,
            SystemRole.LicenseReviewRequired => NeedsReview,
            _ => throw new ArgumentException("ライセンス条件のシステム役割を指定してください。", nameof(role)),
        };
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
