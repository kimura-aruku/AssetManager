using AssetManager.Domain.Catalog;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Application.Records;

public sealed record FileSelectionDefaults(
    string SuggestedName,
    IReadOnlyList<AssetTypeId> SuggestedTypeIds);

public static class FileSelectionDefaultProvider
{
    public static FileSelectionDefaults Create(
        string filePath,
        IEnumerable<AssetTypeDefinition> assetTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(assetTypes);

        var name = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var typeIds = string.IsNullOrWhiteSpace(extension)
            ? []
            : assetTypes
                .Where(type => type.MatchesExtension(extension))
                .Select(type => type.Id)
                .ToArray();
        return new FileSelectionDefaults(name, typeIds);
    }
}
