using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization;

/// <summary>
/// Serializes typed FHIR resources into an external representation.
/// </summary>
public interface IFhirSerializer
{
    /// <summary>
    /// Serializes a typed FHIR resource.
    /// </summary>
    /// <typeparam name="TResource">The concrete FHIR resource type.</typeparam>
    /// <param name="resource">The resource instance to serialize.</param>
    /// <returns>The serialized resource payload.</returns>
    string Serialize<TResource>(TResource resource)
        where TResource : Resource;
}
