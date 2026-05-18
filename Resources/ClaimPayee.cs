using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Recipient of benefits payable for a FHIR R5 Claim.
/// </summary>
public sealed class ClaimPayee : BackboneElement
{
    /// <summary>
    /// Category of recipient.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Recipient reference.
    /// </summary>
    public Reference? Party { get; set; }
}
