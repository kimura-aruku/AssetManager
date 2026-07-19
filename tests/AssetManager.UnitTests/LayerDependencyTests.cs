using AssetManager.Application;
using AssetManager.Domain;

namespace AssetManager.UnitTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void DomainDoesNotReferenceOtherSolutionLayers()
    {
        var references = GetSolutionLayerReferences(typeof(DomainAssemblyMarker));

        Assert.Empty(references);
    }

    [Fact]
    public void ApplicationReferencesOnlyDomain()
    {
        _ = ApplicationAssemblyMarker.DomainMarkerType;
        var references = GetSolutionLayerReferences(typeof(ApplicationAssemblyMarker));

        Assert.Equal(["AssetManager.Domain"], references);
    }

    private static string[] GetSolutionLayerReferences(Type markerType)
    {
        return markerType.Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("AssetManager.", StringComparison.Ordinal))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
