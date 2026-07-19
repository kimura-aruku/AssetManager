using AssetManager.Domain;

namespace AssetManager.Application;

/// <summary>
/// Provides stable assembly metadata for the Application layer.
/// </summary>
public static class ApplicationAssemblyMarker
{
    /// <summary>
    /// Gets a Domain type to make the intended layer dependency explicit.
    /// </summary>
    public static Type DomainMarkerType => typeof(DomainAssemblyMarker);
}
