using System.Collections.ObjectModel;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Values;

public abstract record FieldValue
{
    public abstract FieldType Type { get; }
}

public sealed record TextFieldValue : FieldValue
{
    public TextFieldValue(string value)
    {
        Value = ValueValidation.RequireText(value, nameof(value));
    }

    public string Value { get; }

    public override FieldType Type => FieldType.Text;
}

public sealed record MultilineTextFieldValue : FieldValue
{
    public MultilineTextFieldValue(string value)
    {
        Value = ValueValidation.RequireText(value, nameof(value));
    }

    public string Value { get; }

    public override FieldType Type => FieldType.MultilineText;
}

public sealed record NumberFieldValue(decimal Value) : FieldValue
{
    public override FieldType Type => FieldType.Number;
}

public sealed record DateFieldValue(AssetDate Value) : FieldValue
{
    public override FieldType Type => FieldType.Date;
}

public sealed record BooleanFieldValue(bool Value) : FieldValue
{
    public override FieldType Type => FieldType.Boolean;
}

public sealed record UrlFieldValue : FieldValue
{
    public UrlFieldValue(string value)
    {
        Value = ValueValidation.RequireWebUrl(value, nameof(value));
    }

    public string Value { get; }

    public override FieldType Type => FieldType.Url;
}

public sealed record SingleSelectionFieldValue(SelectionOptionId Value) : FieldValue
{
    public override FieldType Type => FieldType.SingleSelect;
}

public sealed record MultiSelectionFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<SelectionOptionId> _values;

    public MultiSelectionFieldValue(IEnumerable<SelectionOptionId> values)
    {
        _values = Array.AsReadOnly(ValueValidation.CopyUnique(values, nameof(values)));
    }

    public IReadOnlyList<SelectionOptionId> Values => _values;

    public override FieldType Type => FieldType.MultiSelect;
}

public sealed record FilePathFieldValue : FieldValue
{
    public FilePathFieldValue(string value)
    {
        Value = ValueValidation.RequireText(value, nameof(value));
    }

    public string Value { get; }

    public override FieldType Type => FieldType.FilePath;
}

public sealed record FolderPathFieldValue : FieldValue
{
    public FolderPathFieldValue(string value)
    {
        Value = ValueValidation.RequireText(value, nameof(value));
    }

    public string Value { get; }

    public override FieldType Type => FieldType.FolderPath;
}

public enum TargetPathKind
{
    File,
    Folder,
}

public sealed record TargetPathFieldValue : FieldValue
{
    public TargetPathFieldValue(TargetPathKind kind, string path)
    {
        Kind = kind;
        Path = ValueValidation.RequireText(path, nameof(path));
    }

    public TargetPathKind Kind { get; }

    public string Path { get; }

    public override FieldType Type => FieldType.TargetPath;
}

public sealed record AssetTypeSetFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<AssetTypeId> _values;

    public AssetTypeSetFieldValue(IEnumerable<AssetTypeId> values)
    {
        _values = Array.AsReadOnly(ValueValidation.CopyUnique(values, nameof(values)));
    }

    public IReadOnlyList<AssetTypeId> Values => _values;

    public override FieldType Type => FieldType.AssetTypeSet;
}

public sealed record TagSetFieldValue : FieldValue
{
    private readonly ReadOnlyCollection<TagId> _values;

    public TagSetFieldValue(IEnumerable<TagId> values)
    {
        _values = Array.AsReadOnly(ValueValidation.CopyUnique(values, nameof(values)));
    }

    public IReadOnlyList<TagId> Values => _values;

    public override FieldType Type => FieldType.TagSet;
}

internal static class ValueValidation
{
    public static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("空でない値を指定してください。", parameterName);
        }

        return value;
    }

    public static string RequireWebUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new DomainValidationException("HTTPまたはHTTPSのURLを指定してください。", parameterName);
        }

        return value;
    }

    public static T[] CopyUnique<T>(IEnumerable<T> values, string parameterName)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(values);
        var copied = values.ToArray();

        if (copied.Distinct().Count() != copied.Length)
        {
            throw new DomainValidationException("同じ値を重複して指定できません。", parameterName);
        }

        return copied;
    }

    public static string[] CopyUniqueText(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copied = values.Select(value => RequireText(value, parameterName)).ToArray();

        if (copied.Distinct(StringComparer.OrdinalIgnoreCase).Count() != copied.Length)
        {
            throw new DomainValidationException("同じ値を重複して指定できません。", parameterName);
        }

        return copied;
    }
}
