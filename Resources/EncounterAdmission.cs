using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Admission details for a FHIR R5 Encounter.
/// </summary>
public sealed class EncounterAdmission : BackboneElement
{
    /// <summary>
    /// Pre-admission identifier.
    /// </summary>
    public Identifier? PreAdmissionIdentifier { get; set; }

    /// <summary>
    /// Location or organization from which the patient came before admission.
    /// </summary>
    public Reference? Origin { get; set; }

    /// <summary>
    /// Source from which the patient was admitted.
    /// </summary>
    public CodeableConcept? AdmitSource { get; set; }

    /// <summary>
    /// Indicates that the patient is being re-admitted.
    /// </summary>
    public CodeableConcept? ReAdmission { get; set; }

    /// <summary>
    /// Location or organization to which the patient is discharged.
    /// </summary>
    public Reference? Destination { get; set; }

    /// <summary>
    /// Category or kind of location after discharge.
    /// </summary>
    public CodeableConcept? DischargeDisposition { get; set; }
}
