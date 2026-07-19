using AssetManager.Domain.Catalog;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Records;

public sealed record FileSelectionDefaults(
    string SuggestedName,
    IReadOnlyList<AssetTypeId> SuggestedTypeIds);

public static class FileSelectionDefaultProvider
{
    public static FileSelectionDefaults Create(
        string targetPath,
        TargetPathKind targetKind,
        IEnumerable<AssetTypeDefinition> assetTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(assetTypes);

        var pathWithoutTrailingSeparator = Path.TrimEndingDirectorySeparator(targetPath);
        var name = targetKind == TargetPathKind.Folder
            ? Path.GetFileName(pathWithoutTrailingSeparator)
            : Path.GetFileNameWithoutExtension(pathWithoutTrailingSeparator);
        var extension = targetKind == TargetPathKind.File
            ? Path.GetExtension(pathWithoutTrailingSeparator)
            : string.Empty;
        var typeIds = extension.Length == 0
            ? []
            : assetTypes
                .Where(type => type.MatchesExtension(extension))
                .Select(type => type.Id)
                .ToArray();
        return new FileSelectionDefaults(name, typeIds);
    }
}
