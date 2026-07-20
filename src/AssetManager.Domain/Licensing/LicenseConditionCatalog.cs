using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Licensing;

public sealed record LicenseConditionDefinition(
    FieldId FieldId,
    SystemRole SystemRole,
    string Label,
    string Summary,
    string Description,
    string Glyph,
    bool IsRequired)
{
    public string ToolTip => $"{Summary}{Environment.NewLine}{Description}";
}

public static class LicenseConditionCatalog
{
    public static IReadOnlyList<LicenseConditionDefinition> All { get; } = Array.AsReadOnly(
        new LicenseConditionDefinition[]
    {
        new(BuiltInFieldIds.CommercialUseAllowed, SystemRole.CommercialUseAllowed,
            "商用利用可", "商用利用できる", "営利目的の製品・サービスで利用できます。", "商", false),
        new(BuiltInFieldIds.ModificationAllowed, SystemRole.ModificationAllowed,
            "改変可", "内容を変更できる", "元データやソースコードを編集・加工できます。", "改", false),
        new(BuiltInFieldIds.ProductEmbeddingAllowed, SystemRole.ProductEmbeddingAllowed,
            "製品組込み可", "製品に組み込める", "ゲームやアプリなどの製品に組み込んで利用・配布できます。", "組", false),
        new(BuiltInFieldIds.OriginalDataRedistributionAllowed, SystemRole.OriginalDataRedistributionAllowed,
            "元データ再配布可", "元データを再配布できる", "元データを単体または取り出せる形で第三者に配布できます。", "再", false),
        new(BuiltInFieldIds.CreditDisplayRequired, SystemRole.CreditDisplayRequired,
            "クレジット表示必須", "利用者向けの作者表示が必要", "クレジット画面など、利用者が確認できる場所への作者名等の表示が必要です。", "©", true),
        new(BuiltInFieldIds.CopyrightNoticeRetentionRequired, SystemRole.CopyrightNoticeRetentionRequired,
            "著作権表示保持必須", "Copyright表記を残す必要がある", "元データに含まれる著作権表示を削除せず保持する必要があります。", "C", true),
        new(BuiltInFieldIds.LicenseTextAttachmentRequired, SystemRole.LicenseTextAttachmentRequired,
            "ライセンス文添付必須", "ライセンス本文の添付が必要", "配布物にライセンス本文または指定された許諾表示を含める必要があります。", "文", true),
        new(BuiltInFieldIds.SameLicenseRequired, SystemRole.SameLicenseRequired,
            "同一ライセンス継承必須", "派生物にも同じライセンスを適用する必要がある", "改変物や派生物を配布する場合、同一または指定されたライセンスの適用が必要です。", "継", true),
        new(BuiltInFieldIds.AiTrainingAllowed, SystemRole.AiTrainingAllowed,
            "AI学習利用可", "AIモデルの学習に利用できる", "AI・機械学習モデルの学習データとして利用できます。", "学", false),
        new(BuiltInFieldIds.GenerativeAiInputAllowed, SystemRole.GenerativeAiInputAllowed,
            "生成AI入力可", "生成AIへ入力できる", "生成AIのプロンプト、参照画像、入力データなどとして利用できます。", "AI", false),
        new(BuiltInFieldIds.EngineRestrictionExists, SystemRole.EngineRestrictionExists,
            "エンジン制限あり", "特定のエンジンでのみ利用できる", "Unity限定など、利用できるゲームエンジンや環境に制限があります。", "制", true),
        new(BuiltInFieldIds.LicenseReviewRequired, SystemRole.LicenseReviewRequired,
            "再確認必要", "利用前の再確認が必要", "規約変更、個別条件、用途制限などを利用前に再確認する必要があります。", "確", true),
    });

    public static LicenseConditionDefinition? Find(SystemRole? role)
    {
        return role is null ? null : All.FirstOrDefault(condition => condition.SystemRole == role);
    }
}
