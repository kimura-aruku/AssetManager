using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Fields;

public static class BuiltInFieldIds
{
    public static readonly FieldId Name = new("builtin.name");
    public static readonly FieldId AssetTypes = new("builtin.assetTypes");
    public static readonly FieldId TargetPath = new("builtin.targetPath");
    public static readonly FieldId Tags = new("builtin.tags");
    public static readonly FieldId Status = new("builtin.status");
    public static readonly FieldId Favorite = new("builtin.favorite");
    public static readonly FieldId Overview = new("builtin.overview");
    public static readonly FieldId Description = new("builtin.description");
    // 既存データを「詳細」へ移行するために旧IDを保持する。標準カタログには含めない。
    public static readonly FieldId Notes = new("builtin.notes");
    public static readonly FieldId CurrentVersion = new("builtin.currentVersion");
    public static readonly FieldId AssetUpdatedDate = new("builtin.assetUpdatedDate");
    public static readonly FieldId Creators = new("builtin.creators");
    public static readonly FieldId Sellers = new("builtin.sellers");
    public static readonly FieldId AcquisitionSource = new("builtin.acquisitionSource");
    public static readonly FieldId ProductUrl = new("builtin.productUrl");
    public static readonly FieldId AcquiredDate = new("builtin.acquiredDate");
    public static readonly FieldId PriceJpy = new("builtin.priceJpy");
    public static readonly FieldId OrderNumber = new("builtin.orderNumber");
    public static readonly FieldId ReceiptPath = new("builtin.receiptPath");
    public static readonly FieldId PurchaseAccount = new("builtin.purchaseAccount");
    public static readonly FieldId CreditRequired = new("builtin.creditRequired");
    public static readonly FieldId LinkRequired = new("builtin.linkRequired");
    public static readonly FieldId LogoRequired = new("builtin.logoRequired");
    public static readonly FieldId CommercialUseAllowed = new("builtin.commercialUseAllowed");
    public static readonly FieldId ModificationAllowed = new("builtin.modificationAllowed");
    public static readonly FieldId RedistributionAllowed = new("builtin.redistributionAllowed");
    public static readonly FieldId AdultUseAllowed = new("builtin.adultUseAllowed");
    public static readonly FieldId GenerativeAiUseAllowed = new("builtin.generativeAiUseAllowed");
    public static readonly FieldId LicenseUnknown = new("builtin.licenseUnknown");
    public static readonly FieldId LicenseNeedsReview = new("builtin.licenseNeedsReview");
    public static readonly FieldId CreditText = new("builtin.creditText");
    public static readonly FieldId LicenseNotes = new("builtin.licenseNotes");
    public static readonly FieldId LicenseFilePath = new("builtin.licenseFilePath");
    public static readonly FieldId LicenseUrl = new("builtin.licenseUrl");
    public static readonly FieldId LicenseLastCheckedDate = new("builtin.licenseLastCheckedDate");
    public static readonly FieldId LicenseExpiryDate = new("builtin.licenseExpiryDate");
    public static readonly FieldId RelatedDocuments = new("builtin.relatedDocuments");
    public static readonly FieldId RelatedUrls = new("builtin.relatedUrls");
}
