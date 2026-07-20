namespace AssetManager.Domain.Fields;

public enum FieldOrigin
{
    BuiltIn,
    Custom,
}

public enum FieldType
{
    Text,
    MultilineText,
    Number,
    Date,
    Boolean,
    Url,
    SingleSelect,
    MultiSelect,
    FilePath,
    FolderPath,
    TargetPath,
    StringList,
    TitledPathList,
    TitledUrlList,
    AssetTypeSet,
    TagSet,
    RecordStatus,
}

public enum SystemRole
{
    RecordName,
    AssetTypes,
    TargetPath,
    Tags,
    Status,
    Favorite,
    Overview,
    Description,
    // 既存データ移行専用。新しい標準カラムには使用しない。
    Notes,
    CurrentVersion,
    AssetUpdatedDate,
    Creators,
    Sellers,
    AcquisitionSource,
    ProductUrl,
    AcquiredDate,
    PriceJpy,
    OrderNumber,
    ReceiptPath,
    PurchaseAccount,
    LicensePreset,
    CreditRequired,
    LinkRequired,
    LogoRequired,
    CommercialUseAllowed,
    ModificationAllowed,
    RedistributionAllowed,
    AdultUseAllowed,
    GenerativeAiUseAllowed,
    LicenseUnknown,
    LicenseNeedsReview,
    CreditText,
    LicenseNotes,
    LicenseFilePath,
    LicenseUrl,
    LicenseLastCheckedDate,
    LicenseExpiryDate,
    RelatedDocuments,
    RelatedUrls,
}
