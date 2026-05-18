using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Prior or corollary claim related to a FHIR R5 Claim.
/// </summary>
public sealed class ClaimRelated : BackboneElement
{
    /// <summary>
    /// Reference to the related claim.
    /// </summary>
    public Reference? Claim { get; set; }

    /// <summary>
    /// How the referenced claim is related.
    /// </summary>
    public CodeableConcept? Relationship { get; set; }

    /// <summary>
    /// File or case reference.
    /// </summary>
    public Identifier? Reference { get; set; }
}
