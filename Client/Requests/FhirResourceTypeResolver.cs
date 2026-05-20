using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Requests;

/// <summary>
/// Resolves the FHIR REST resource type segment for SDK resource classes.
/// </summary>
public sealed class FhirResourceTypeResolver
{
    /// <summary>
    /// Resolves a resource type from a generic SDK resource type.
    /// </summary>
    public string GetResourceType<TResource>()
        where TResource : Resource
    {
        var resourceType = typeof(TResource).Name;
        return EnsureResourceType(resourceType);
    }

    /// <summary>
    /// Resolves a resource type from an SDK resource instance.
    /// </summary>
    public string GetResourceType(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var resourceType = string.IsNullOrWhiteSpace(resource.ResourceType)
            ? resource.GetType().Name
            : resource.ResourceType;

        return EnsureResourceType(resourceType);
    }

    private static string EnsureResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new ArgumentException("FHIR resource type cannot be empty.", nameof(resourceType));
        }

        return resourceType;
    }
}
