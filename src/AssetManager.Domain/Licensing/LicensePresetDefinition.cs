using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Licensing;

public sealed record LicensePresetDefinition
{
    public LicensePresetDefinition(
        LicensePresetId id,
        string name,
        LicenseTerms terms)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("定型ライセンス名を指定してください。", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Terms = terms ?? throw new ArgumentNullException(nameof(terms));
    }

    public LicensePresetId Id { get; }

    public string Name { get; }

    public LicenseTerms Terms { get; }
}
