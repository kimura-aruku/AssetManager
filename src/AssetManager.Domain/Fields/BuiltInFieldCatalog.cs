using System.Collections.ObjectModel;
using AssetManager.Domain.Licensing;

namespace AssetManager.Domain.Fields;

public static class BuiltInFieldCatalog
{
    public static IReadOnlyList<FieldDefinition> All { get; } = Array.AsReadOnly(CreateAll());

    private static FieldDefinition[] CreateAll()
    {
        return
        [
            BuiltIn(BuiltInFieldIds.Name, "素材名", FieldType.Text, SystemRole.RecordName, required: true),
            BuiltIn(BuiltInFieldIds.AssetTypes, "種類", FieldType.AssetTypeSet, SystemRole.AssetTypes, required: true),
            BuiltIn(BuiltInFieldIds.TargetPath, "対象パス", FieldType.TargetPath, SystemRole.TargetPath, required: true),
            BuiltIn(BuiltInFieldIds.Tags, "タグ", FieldType.TagSet, SystemRole.Tags),
            BuiltIn(BuiltInFieldIds.Status, "状態", FieldType.RecordStatus, SystemRole.Status),
            BuiltIn(BuiltInFieldIds.Favorite, "お気に入り", FieldType.Boolean, SystemRole.Favorite),
            BuiltIn(BuiltInFieldIds.Overview, "概要", FieldType.MultilineText, SystemRole.Overview),
            BuiltIn(BuiltInFieldIds.Description, "詳細", FieldType.MultilineText, SystemRole.Description),
            BuiltIn(BuiltInFieldIds.CurrentVersion, "現在のバージョン", FieldType.Text, SystemRole.CurrentVersion),
            BuiltIn(BuiltInFieldIds.AssetUpdatedDate, "素材最終更新日", FieldType.Date, SystemRole.AssetUpdatedDate),
            BuiltIn(BuiltInFieldIds.Creators, "制作者", FieldType.StringList, SystemRole.Creators),
            BuiltIn(BuiltInFieldIds.Sellers, "販売者", FieldType.StringList, SystemRole.Sellers),
            BuiltIn(BuiltInFieldIds.AcquisitionSource, "購入／入手元", FieldType.SingleSelect, SystemRole.AcquisitionSource),
            BuiltIn(BuiltInFieldIds.ProductUrl, "商品URL", FieldType.Url, SystemRole.ProductUrl),
            BuiltIn(BuiltInFieldIds.AcquiredDate, "購入／入手日", FieldType.Date, SystemRole.AcquiredDate),
            BuiltIn(BuiltInFieldIds.PriceJpy, "購入価格（日本円）", FieldType.Number, SystemRole.PriceJpy),
            BuiltIn(BuiltInFieldIds.OrderNumber, "注文番号", FieldType.Text, SystemRole.OrderNumber),
            BuiltIn(BuiltInFieldIds.ReceiptPath, "領収書パス", FieldType.FilePath, SystemRole.ReceiptPath),
            BuiltIn(BuiltInFieldIds.PurchaseAccount, "購入アカウント名", FieldType.Text, SystemRole.PurchaseAccount),
            BuiltIn(BuiltInFieldIds.LicensePreset, "定型ライセンス", FieldType.SingleSelect, SystemRole.LicensePreset),
            ..LicenseConditionCatalog.All.Select(condition => BuiltIn(
                condition.FieldId,
                condition.Label,
                FieldType.Boolean,
                condition.SystemRole)),
            BuiltIn(BuiltInFieldIds.CreditText, "クレジット内容", FieldType.MultilineText, SystemRole.CreditText),
            BuiltIn(BuiltInFieldIds.LicenseNotes, "ライセンス備考", FieldType.MultilineText, SystemRole.LicenseNotes),
            BuiltIn(BuiltInFieldIds.LicenseFilePath, "ライセンスファイルパス", FieldType.FilePath, SystemRole.LicenseFilePath),
            BuiltIn(BuiltInFieldIds.LicenseUrl, "ライセンスURL", FieldType.Url, SystemRole.LicenseUrl),
            BuiltIn(BuiltInFieldIds.LicenseLastCheckedDate, "ライセンス最終確認日", FieldType.Date, SystemRole.LicenseLastCheckedDate),
            BuiltIn(BuiltInFieldIds.LicenseExpiryDate, "ライセンス期限", FieldType.Date, SystemRole.LicenseExpiryDate),
            BuiltIn(BuiltInFieldIds.RelatedDocuments, "関連文書", FieldType.TitledPathList, SystemRole.RelatedDocuments),
            BuiltIn(BuiltInFieldIds.RelatedUrls, "関連URL", FieldType.TitledUrlList, SystemRole.RelatedUrls),
        ];
    }

    private static FieldDefinition BuiltIn(
        Identifiers.FieldId id,
        string label,
        FieldType type,
        SystemRole role,
        bool required = false)
    {
        return FieldDefinition.CreateBuiltIn(
            id,
            label,
            type,
            role,
            mainTableVisible: required,
            mainTableRequired: required,
            userCanHide: !required);
    }
}
