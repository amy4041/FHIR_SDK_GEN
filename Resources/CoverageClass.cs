using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Classification information for a FHIR R5 Coverage resource.
/// </summary>
public sealed class CoverageClass : BackboneElement
{
    /// <summary>
    /// Type of coverage classification, such as group or plan.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Classification value assigned by the issuer.
    /// </summary>
    public Identifier? Value { get; set; }

    /// <summary>
    /// Human-readable class name.
    /// </summary>
    public FhirString? Name { get; set; }
}
