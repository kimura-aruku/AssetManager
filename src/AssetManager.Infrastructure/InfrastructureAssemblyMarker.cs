using AssetManager.Application;
using AssetManager.Domain;

namespace AssetManager.Infrastructure;

/// <summary>
/// Provides stable assembly metadata for the Infrastructure layer.
/// </summary>
public static class InfrastructureAssemblyMarker
{
    /// <summary>
    /// Gets the solution-layer types that Infrastructure is allowed to use.
    /// </summary>
    public static IReadOnlyCollection<Type> AllowedLayerTypes { get; } =
    [
        typeof(ApplicationAssemblyMarker),
        typeof(DomainAssemblyMarker),
    ];
}
