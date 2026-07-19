using AssetManager.Infrastructure;
using AssetManager.Application;

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

    [Theory]
    [InlineData(typeof(ApplicationAssemblyMarker))]
    [InlineData(typeof(InfrastructureAssemblyMarker))]
    public void CoreAssembliesDoNotReferenceExternalTransmissionLibraries(Type markerType)
    {
        var references = markerType.Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", references);
        Assert.DoesNotContain(references, name =>
            name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ApplicationInsights", StringComparison.OrdinalIgnoreCase)
            || name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }
}
