using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Anatomical location for a FHIR R5 Claim item.
/// </summary>
public sealed class ClaimBodySite : BackboneElement
{
    /// <summary>
    /// Location represented by a code or BodyStructure reference.
    /// </summary>
    public IList<CodeableReference> Site { get; set; } = new List<CodeableReference>();

    /// <summary>
    /// Anatomical sub-locations.
    /// </summary>
    public IList<CodeableConcept> SubSite { get; set; } = new List<CodeableConcept>();
}
