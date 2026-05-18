using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Location used during a FHIR R4 Encounter.
/// </summary>
public sealed class EncounterLocation : BackboneElement
{
    /// <summary>
    /// Location resource involved in the encounter.
    /// </summary>
    public Reference? Location { get; set; }

    /// <summary>
    /// planned | active | reserved | completed.
    /// </summary>
    public FhirCode? Status { get; set; }

    /// <summary>
    /// Form of location required, such as bed, room, or ward.
    /// </summary>
    public CodeableConcept? Form { get; set; }

    /// <summary>
    /// Time period during which the patient was present at this location.
    /// </summary>
    public Period? Period { get; set; }
}
