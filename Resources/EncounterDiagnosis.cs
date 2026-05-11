using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Diagnosis relevant to a FHIR R4 Encounter.
/// </summary>
public sealed class EncounterDiagnosis : BackboneElement
{
    /// <summary>
    /// Condition or procedure relevant to the encounter.
    /// </summary>
    public Reference? Condition { get; set; }

    /// <summary>
    /// Role or use of this diagnosis.
    /// </summary>
    public CodeableConcept? Use { get; set; }

    /// <summary>
    /// Diagnosis ranking for the encounter.
    /// </summary>
    public FhirInteger? Rank { get; set; }
}
