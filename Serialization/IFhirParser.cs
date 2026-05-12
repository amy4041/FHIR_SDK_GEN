using MyFhirSdk.Core;

namespace MyFhirSdk.Serialization;

/// <summary>
/// Parses an external representation into typed FHIR resources.
/// </summary>
public interface IFhirParser
{
    /// <summary>
    /// Parses a serialized FHIR resource payload.
    /// </summary>
    /// <typeparam name="TResource">The expected concrete FHIR resource type.</typeparam>
    /// <param name="json">The serialized FHIR JSON payload.</param>
    /// <returns>The parsed typed resource instance.</returns>
    TResource Parse<TResource>(string json)
        where TResource : Resource;
}
