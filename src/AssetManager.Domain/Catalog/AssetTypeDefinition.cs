using System.Collections.ObjectModel;
using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Catalog;

public sealed class AssetTypeDefinition
{
    private readonly ReadOnlyCollection<string> _extensions;

    public AssetTypeDefinition(
        AssetTypeId id,
        string name,
        IEnumerable<string>? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("種類名を指定してください。", nameof(name));
        }

        Id = id;
        Name = name;
        var copiedExtensions = (extensions ?? [])
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _extensions = Array.AsReadOnly(copiedExtensions);
    }

    public AssetTypeId Id { get; }

    public string Name { get; }

    public IReadOnlyList<string> Extensions => _extensions;

    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new DomainValidationException("拡張子を指定してください。", nameof(extension));
        }

        var normalized = extension.StartsWith('.')
            ? extension
            : $".{extension}";

        if (normalized.Length == 1
            || normalized.Contains(Path.DirectorySeparatorChar)
            || normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new DomainValidationException("拡張子の形式が正しくありません。", nameof(extension));
        }

        return normalized.ToLowerInvariant();
    }

    public bool MatchesExtension(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return _extensions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
