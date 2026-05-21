using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Diagnosis relevant to a FHIR R5 Encounter.
/// </summary>
public sealed class EncounterDiagnosis : BackboneElement
{
    /// <summary>
    /// Diagnoses relevant to the encounter.
    /// </summary>
    public IList<CodeableReference> Condition { get; set; } = new List<CodeableReference>();

    /// <summary>
    /// Roles this diagnosis has within the encounter.
    /// </summary>
    public IList<CodeableConcept> Use { get; set; } = new List<CodeableConcept>();
}
