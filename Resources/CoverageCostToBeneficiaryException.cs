using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Exception for patient payments under a FHIR R5 Coverage.
/// </summary>
public sealed class CoverageCostToBeneficiaryException : BackboneElement
{
    /// <summary>
    /// Exception category.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Effective period of the exception.
    /// </summary>
    public Period? Period { get; set; }
}
