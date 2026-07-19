using AssetManager.Domain.Common;
using System.Collections.ObjectModel;

namespace AssetManager.Domain.Fields;

public readonly record struct MainTableColumnId
{
    public MainTableColumnId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("表示カラムIDを指定してください。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static class MainTableColumns
{
    public static readonly MainTableColumnId Name = new(BuiltInFieldIds.Name.Value);
    public static readonly MainTableColumnId AssetTypes = new(BuiltInFieldIds.AssetTypes.Value);
    public static readonly MainTableColumnId TargetPath = new(BuiltInFieldIds.TargetPath.Value);
    public static readonly MainTableColumnId LicenseSummary = new("computed.licenseSummary");

    public static IReadOnlyList<MainTableColumnId> Required { get; } = Array.AsReadOnly(
        new[]
        {
            Name,
            AssetTypes,
            TargetPath,
            LicenseSummary,
        });
}
