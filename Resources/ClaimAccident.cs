using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Accident details for a FHIR R5 Claim.
/// </summary>
public sealed class ClaimAccident : BackboneElement
{
    /// <summary>
    /// When the incident occurred.
    /// </summary>
    public FhirDate? Date { get; set; }

    /// <summary>
    /// Nature of the accident.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Accident location represented by an address.
    /// </summary>
    public Address? LocationAddress { get; set; }

    /// <summary>
    /// Accident location represented by a reference.
    /// </summary>
    public Reference? LocationReference { get; set; }
}
