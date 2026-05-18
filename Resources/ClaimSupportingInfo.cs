using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Supporting information for a FHIR R5 Claim.
/// </summary>
public sealed class ClaimSupportingInfo : BackboneElement
{
    /// <summary>
    /// Supporting information sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Classification of the supplied information.
    /// </summary>
    public CodeableConcept? Category { get; set; }

    /// <summary>
    /// Type of information.
    /// </summary>
    public CodeableConcept? Code { get; set; }

    /// <summary>
    /// Date when the information occurred.
    /// </summary>
    public FhirDate? TimingDate { get; set; }

    /// <summary>
    /// Period when the information occurred.
    /// </summary>
    public Period? TimingPeriod { get; set; }

    /// <summary>
    /// Boolean value to be provided.
    /// </summary>
    public FhirBoolean? ValueBoolean { get; set; }

    /// <summary>
    /// String value to be provided.
    /// </summary>
    public FhirString? ValueString { get; set; }

    /// <summary>
    /// Quantity value to be provided.
    /// </summary>
    public Quantity? ValueQuantity { get; set; }

    /// <summary>
    /// Attachment value to be provided.
    /// </summary>
    public Attachment? ValueAttachment { get; set; }

    /// <summary>
    /// Reference value to be provided.
    /// </summary>
    public Reference? ValueReference { get; set; }

    /// <summary>
    /// Identifier value to be provided.
    /// </summary>
    public Identifier? ValueIdentifier { get; set; }

    /// <summary>
    /// Explanation for the information.
    /// </summary>
    public CodeableConcept? Reason { get; set; }
}
