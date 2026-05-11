using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Language preference for communicating with a FHIR R4 Patient.
/// </summary>
public sealed class PatientCommunication : BackboneElement
{
    /// <summary>
    /// Language used for communication.
    /// </summary>
    public CodeableConcept? Language { get; set; }

    /// <summary>
    /// Whether this language is preferred.
    /// </summary>
    public FhirBoolean? Preferred { get; set; }
}
