using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Diagnosis information on a FHIR R4 Claim.
/// </summary>
public sealed class ClaimDiagnosis : BackboneElement
{
    /// <summary>
    /// Diagnosis sequence number.
    /// </summary>
    public FhirInteger? Sequence { get; set; }

    /// <summary>
    /// Coded diagnosis.
    /// </summary>
    public CodeableConcept? DiagnosisCodeableConcept { get; set; }

    /// <summary>
    /// Diagnosis represented by another resource.
    /// </summary>
    public Reference? DiagnosisReference { get; set; }

    /// <summary>
    /// Diagnosis type, such as admitting or principal.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();
}
