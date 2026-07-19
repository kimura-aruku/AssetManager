using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Domain.Validation;

public enum ValidationIssueCode
{
    DuplicateFieldId,
    DuplicateSystemRole,
    MissingRequiredField,
    InvalidRequiredFieldConstraint,
    InvalidBuiltInFieldDefinition,
    FieldTypeMismatch,
    InvalidSelectionOptionReference,
    InvalidAssetTypeReference,
    InvalidTagReference,
    InvalidTagCategoryReference,
    InvalidDedicatedValue,
}

public sealed record ValidationIssue(
    ValidationIssueCode Code,
    string Message,
    FieldId? FieldId = null);

public sealed record AssetRecordValidationResult(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<FieldId> UnknownFieldIds)
{
    public bool IsValid => Issues.Count == 0;
}

public static class DomainModelValidator
{
    private static readonly FieldId[] RequiredStoredFieldIds =
    [
        BuiltInFieldIds.Name,
        BuiltInFieldIds.AssetTypes,
        BuiltInFieldIds.TargetPath,
    ];

    public static IReadOnlyList<ValidationIssue> ValidateFieldDefinitions(
        IEnumerable<FieldDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var copied = definitions.ToArray();
        var issues = new List<ValidationIssue>();

        foreach (var duplicate in copied.GroupBy(definition => definition.Id).Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCode.DuplicateFieldId,
                $"カラムID'{duplicate.Key}'が重複しています。",
                duplicate.Key));
        }

        foreach (var duplicate in copied
                     .Where(definition => definition.SystemRole is not null)
                     .GroupBy(definition => definition.SystemRole)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCode.DuplicateSystemRole,
                $"システム役割'{duplicate.Key}'が重複しています。"));
        }

        foreach (var requiredId in RequiredStoredFieldIds)
        {
            var definition = copied.FirstOrDefault(candidate => candidate.Id == requiredId);
            if (definition is null)
            {
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.MissingRequiredField,
                    $"中央固定カラム'{requiredId}'がありません。",
                    requiredId));
                continue;
            }

            if (!definition.MainTableVisible
                || !definition.MainTableRequired
                || definition.UserCanHide
                || definition.UserCanRename
                || definition.UserCanChangeType
                || definition.UserCanDelete)
            {
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.InvalidRequiredFieldConstraint,
                    $"中央固定カラム'{requiredId}'の制約が正しくありません。",
                    requiredId));
            }
        }

        var canonicalDefinitions = BuiltInFieldCatalog.All.ToDictionary(definition => definition.Id);
        foreach (var definition in copied.Where(definition => definition.Origin == FieldOrigin.BuiltIn))
        {
            if (!canonicalDefinitions.TryGetValue(definition.Id, out var canonical)
                || (!HasCanonicalFixedDefinition(definition, canonical)
                    && !IsLegacyAcquisitionSourceDefinition(definition, canonical)))
            {
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.InvalidBuiltInFieldDefinition,
                    $"標準カラム'{definition.Id}'の固定定義が変更されています。",
                    definition.Id));
            }
        }

        return issues;
    }

    private static bool HasCanonicalFixedDefinition(
        FieldDefinition definition,
        FieldDefinition canonical)
    {
        return definition.Label == canonical.Label
            && definition.Type == canonical.Type
            && definition.SystemRole == canonical.SystemRole
            && definition.MainTableRequired == canonical.MainTableRequired
            && definition.UserCanHide == canonical.UserCanHide
            && definition.UserCanRename == canonical.UserCanRename
            && definition.UserCanChangeType == canonical.UserCanChangeType
            && definition.UserCanDelete == canonical.UserCanDelete;
    }

    private static bool IsLegacyAcquisitionSourceDefinition(
        FieldDefinition definition,
        FieldDefinition canonical)
    {
        return definition.Id == BuiltInFieldIds.AcquisitionSource
            && definition.Type == FieldType.Text
            && canonical.Type == FieldType.SingleSelect
            && definition.Label == canonical.Label
            && definition.SystemRole == canonical.SystemRole
            && definition.MainTableRequired == canonical.MainTableRequired
            && definition.UserCanHide == canonical.UserCanHide
            && definition.UserCanRename == canonical.UserCanRename
            && definition.UserCanChangeType == canonical.UserCanChangeType
            && definition.UserCanDelete == canonical.UserCanDelete;
    }

    public static AssetRecordValidationResult ValidateRecord(
        AssetRecord record,
        IEnumerable<FieldDefinition> definitions,
        IEnumerable<AssetTypeDefinition> assetTypes,
        IEnumerable<TagDefinition> tags)
    {
        ArgumentNullException.ThrowIfNull(record);
        var definitionMap = definitions
            .GroupBy(definition => definition.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var assetTypeIds = assetTypes.Select(assetType => assetType.Id).ToHashSet();
        var tagIds = tags.Select(tag => tag.Id).ToHashSet();
        var issues = new List<ValidationIssue>();
        var unknownFieldIds = new List<FieldId>();

        foreach (var (fieldId, value) in record.Values)
        {
            if (!definitionMap.TryGetValue(fieldId, out var definition))
            {
                unknownFieldIds.Add(fieldId);
                continue;
            }

            ValidateFieldValue(definition, value, assetTypeIds, tagIds, issues);
        }

        return new AssetRecordValidationResult(issues, unknownFieldIds);
    }

    public static IReadOnlyList<ValidationIssue> ValidateTagCategories(
        IEnumerable<TagDefinition> tags,
        IEnumerable<TagCategoryDefinition> categories)
    {
        var categoryIds = categories.Select(category => category.Id).ToHashSet();

        return tags
            .Where(tag => tag.CategoryId is { } categoryId && !categoryIds.Contains(categoryId))
            .Select(tag => new ValidationIssue(
                ValidationIssueCode.InvalidTagCategoryReference,
                $"タグ'{tag.Name}'が未定義の分類を参照しています。"))
            .ToArray();
    }

    private static void ValidateFieldValue(
        FieldDefinition definition,
        FieldValue value,
        HashSet<AssetTypeId> assetTypeIds,
        HashSet<TagId> tagIds,
        List<ValidationIssue> issues)
    {
        if (definition.Type != value.Type)
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCode.FieldTypeMismatch,
                $"カラム'{definition.Label}'の値型が一致しません。",
                definition.Id));
            return;
        }

        ValidateDedicatedValue(definition, value, issues);
        ValidateSelectionReferences(definition, value, issues);

        if (value is AssetTypeSetFieldValue assetTypeSet)
        {
            foreach (var invalidId in assetTypeSet.Values.Where(id => !assetTypeIds.Contains(id)))
            {
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.InvalidAssetTypeReference,
                    $"未定義の種類ID'{invalidId}'を参照しています。",
                    definition.Id));
            }
        }

        if (value is TagSetFieldValue tagSet)
        {
            foreach (var invalidId in tagSet.Values.Where(id => !tagIds.Contains(id)))
            {
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.InvalidTagReference,
                    $"未定義のタグID'{invalidId}'を参照しています。",
                    definition.Id));
            }
        }
    }

    private static void ValidateDedicatedValue(
        FieldDefinition definition,
        FieldValue value,
        List<ValidationIssue> issues)
    {
        var valid = definition.SystemRole switch
        {
            SystemRole.Creators => value is CreatorListFieldValue,
            SystemRole.Sellers => value is SellerListFieldValue,
            _ => true,
        };

        if (!valid)
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCode.InvalidDedicatedValue,
                $"カラム'{definition.Label}'には専用の値型が必要です。",
                definition.Id));
        }
    }

    private static void ValidateSelectionReferences(
        FieldDefinition definition,
        FieldValue value,
        List<ValidationIssue> issues)
    {
        if (value is SingleSelectionFieldValue single
            && definition.Options.All(option => option.Id != single.Value))
        {
            AddInvalidSelectionIssue(definition, single.Value, issues);
        }

        if (value is MultiSelectionFieldValue multiple)
        {
            var optionIds = definition.Options.Select(option => option.Id).ToHashSet();
            foreach (var invalidId in multiple.Values.Where(id => !optionIds.Contains(id)))
            {
                AddInvalidSelectionIssue(definition, invalidId, issues);
            }
        }
    }

    private static void AddInvalidSelectionIssue(
        FieldDefinition definition,
        SelectionOptionId invalidId,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            ValidationIssueCode.InvalidSelectionOptionReference,
            $"未定義の選択肢ID'{invalidId}'を参照しています。",
            definition.Id));
    }
}
