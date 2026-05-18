using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Medical reason expected to be addressed during a FHIR R5 Encounter.
/// </summary>
public sealed class EncounterReason : BackboneElement
{
    /// <summary>
    /// What the reason value should be used for or as.
    /// </summary>
    public IList<CodeableConcept> Use { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Reason the encounter takes place, represented by a code or reference.
    /// </summary>
    public IList<CodeableReference> Value { get; set; } = new List<CodeableReference>();
}
