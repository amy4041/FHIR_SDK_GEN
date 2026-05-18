using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Clinical procedure relevant to a FHIR R5 Claim.
/// </summary>
public sealed class ClaimProcedure : BackboneElement
{
    /// <summary>
    /// Procedure sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Category of procedure.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// When the procedure was performed.
    /// </summary>
    public FhirDateTime? Date { get; set; }

    /// <summary>
    /// Procedure represented by a code.
    /// </summary>
    public CodeableConcept? ProcedureCodeableConcept { get; set; }

    /// <summary>
    /// Procedure represented by a resource reference.
    /// </summary>
    public Reference? ProcedureReference { get; set; }

    /// <summary>
    /// Unique device identifiers.
    /// </summary>
    public IList<Reference> Udi { get; set; } = new List<Reference>();
}
