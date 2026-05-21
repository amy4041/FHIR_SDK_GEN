using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Participant involved in a FHIR R5 Encounter.
/// </summary>
public sealed class EncounterParticipant : BackboneElement
{
    /// <summary>
    /// Roles of the participant.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Time period of the participant's involvement.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Individual, device, or service participating in the encounter.
    /// </summary>
    public Reference? Actor { get; set; }
}
