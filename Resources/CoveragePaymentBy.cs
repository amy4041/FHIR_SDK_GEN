using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Self-pay party and responsibility for a FHIR R5 Coverage.
/// </summary>
public sealed class CoveragePaymentBy : BackboneElement
{
    /// <summary>
    /// Party performing self-payment.
    /// </summary>
    public Reference? Party { get; set; }

    /// <summary>
    /// Description of the financial responsibility.
    /// </summary>
    public FhirString? Responsibility { get; set; }
}
