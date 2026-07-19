using System.Collections.ObjectModel;
using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Fields;

public sealed class FieldDefinition
{
    private readonly ReadOnlyCollection<SelectionOption> _options;

    private FieldDefinition(
        FieldId id,
        FieldOrigin origin,
        SystemRole? systemRole,
        string label,
        FieldType type,
        bool mainTableVisible,
        bool mainTableRequired,
        bool detailVisible,
        bool userCanHide,
        bool userCanRename,
        bool userCanChangeType,
        bool userCanDelete,
        IEnumerable<SelectionOption>? options)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainValidationException("カラム名を指定してください。", nameof(label));
        }

        if ((origin == FieldOrigin.BuiltIn) != id.IsBuiltIn)
        {
            throw new DomainValidationException("カラムIDと生成元が一致していません。", nameof(origin));
        }

        if (origin == FieldOrigin.Custom && systemRole is not null)
        {
            throw new DomainValidationException("カスタムカラムにシステム役割は設定できません。", nameof(systemRole));
        }

        if (mainTableRequired
            && (!mainTableVisible || userCanHide || userCanRename || userCanChangeType || userCanDelete))
        {
            throw new DomainValidationException("中央固定カラムの表示制約が正しくありません。", nameof(mainTableRequired));
        }

        if (origin == FieldOrigin.BuiltIn
            && (userCanRename || userCanChangeType || userCanDelete))
        {
            throw new DomainValidationException("標準カラムの名称、型、削除可否は変更できません。", nameof(origin));
        }

        var copiedOptions = options?.ToArray() ?? [];
        ValidateOptions(type, copiedOptions);
        _options = Array.AsReadOnly(copiedOptions);

        Id = id;
        Origin = origin;
        SystemRole = systemRole;
        Label = label;
        Type = type;
        MainTableVisible = mainTableVisible;
        MainTableRequired = mainTableRequired;
        DetailVisible = detailVisible;
        UserCanHide = userCanHide;
        UserCanRename = userCanRename;
        UserCanChangeType = userCanChangeType;
        UserCanDelete = userCanDelete;
    }

    public FieldId Id { get; }

    public FieldOrigin Origin { get; }

    public SystemRole? SystemRole { get; }

    public string Label { get; }

    public FieldType Type { get; }

    public bool MainTableVisible { get; }

    public bool MainTableRequired { get; }

    public bool DetailVisible { get; }

    public bool UserCanHide { get; }

    public bool UserCanRename { get; }

    public bool UserCanChangeType { get; }

    public bool UserCanDelete { get; }

    public IReadOnlyList<SelectionOption> Options => _options;

    public static FieldDefinition CreateBuiltIn(
        FieldId id,
        string label,
        FieldType type,
        SystemRole systemRole,
        bool mainTableVisible = false,
        bool mainTableRequired = false,
        bool detailVisible = true,
        bool userCanHide = true,
        IEnumerable<SelectionOption>? options = null)
    {
        return new FieldDefinition(
            id,
            FieldOrigin.BuiltIn,
            systemRole,
            label,
            type,
            mainTableVisible || mainTableRequired,
            mainTableRequired,
            detailVisible,
            mainTableRequired ? false : userCanHide,
            false,
            false,
            false,
            options);
    }

    public static FieldDefinition CreateCustom(
        CustomFieldId id,
        string label,
        FieldType type,
        bool mainTableVisible = false,
        bool detailVisible = true,
        IEnumerable<SelectionOption>? options = null)
    {
        EnsureCustomType(type);

        return new FieldDefinition(
            FieldId.From(id),
            FieldOrigin.Custom,
            null,
            label,
            type,
            mainTableVisible,
            false,
            detailVisible,
            true,
            true,
            true,
            true,
            options);
    }

    public FieldDefinition Rename(string label)
    {
        if (!UserCanRename)
        {
            throw new DomainValidationException("このカラムの名称は変更できません。", nameof(label));
        }

        return Copy(label: label);
    }

    public FieldDefinition ChangeType(
        FieldType type,
        IEnumerable<SelectionOption>? options = null)
    {
        if (!UserCanChangeType)
        {
            throw new DomainValidationException("このカラムの型は変更できません。", nameof(type));
        }

        EnsureCustomType(type);
        return Copy(type: type, options: options ?? []);
    }

    public FieldDefinition SetVisibility(bool mainTableVisible, bool detailVisible)
    {
        if (!UserCanHide && (!mainTableVisible || !detailVisible))
        {
            throw new DomainValidationException("このカラムは非表示にできません。");
        }

        return Copy(mainTableVisible: mainTableVisible, detailVisible: detailVisible);
    }

    public FieldDefinition SetSelectionOptions(IEnumerable<SelectionOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (Type is not FieldType.SingleSelect and not FieldType.MultiSelect)
        {
            throw new DomainValidationException("選択型カラム以外には選択肢を設定できません。", nameof(options));
        }

        return Copy(options: options);
    }

    private FieldDefinition Copy(
        string? label = null,
        FieldType? type = null,
        bool? mainTableVisible = null,
        bool? detailVisible = null,
        IEnumerable<SelectionOption>? options = null)
    {
        return new FieldDefinition(
            Id,
            Origin,
            SystemRole,
            label ?? Label,
            type ?? Type,
            mainTableVisible ?? MainTableVisible,
            MainTableRequired,
            detailVisible ?? DetailVisible,
            UserCanHide,
            UserCanRename,
            UserCanChangeType,
            UserCanDelete,
            options ?? _options);
    }

    private static void EnsureCustomType(FieldType type)
    {
        if (type is FieldType.TargetPath
            or FieldType.StringList
            or FieldType.TitledPathList
            or FieldType.TitledUrlList
            or FieldType.AssetTypeSet
            or FieldType.TagSet
            or FieldType.RecordStatus)
        {
            throw new DomainValidationException("この型は標準カラム専用です。", nameof(type));
        }
    }

    private static void ValidateOptions(FieldType type, SelectionOption[] options)
    {
        if (type is not FieldType.SingleSelect and not FieldType.MultiSelect && options.Length > 0)
        {
            throw new DomainValidationException("選択型以外のカラムに選択肢は設定できません。", nameof(options));
        }

        if (options.Select(option => option.Id).Distinct().Count() != options.Length)
        {
            throw new DomainValidationException("選択肢IDが重複しています。", nameof(options));
        }
    }
}
