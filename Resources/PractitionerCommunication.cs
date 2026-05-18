using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Language used to communicate with a FHIR R5 Practitioner.
/// </summary>
public sealed class PractitionerCommunication : BackboneElement
{
    /// <summary>
    /// Language code used to communicate with the practitioner.
    /// </summary>
    public CodeableConcept? Language { get; set; }

    /// <summary>
    /// Whether this language is preferred.
    /// </summary>
    public FhirBoolean? Preferred { get; set; }
}
