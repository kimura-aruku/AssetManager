using AssetManager.Infrastructure;

namespace AssetManager.IntegrationTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void InfrastructureReferencesOnlyApplicationAndDomain()
    {
        _ = InfrastructureAssemblyMarker.AllowedLayerTypes;
        var references = typeof(InfrastructureAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("AssetManager.", StringComparison.Ordinal))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["AssetManager.Application", "AssetManager.Domain"],
            references);
    }
}
