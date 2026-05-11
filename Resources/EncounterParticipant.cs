using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Participant involved in a FHIR R4 Encounter.
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
    /// Practitioner, practitioner role, related person, or device involved.
    /// </summary>
    public Reference? Individual { get; set; }
}
