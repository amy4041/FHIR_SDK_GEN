using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Patient payment details for services or products under a FHIR R5 Coverage.
/// </summary>
public sealed class CoverageCostToBeneficiary : BackboneElement
{
    /// <summary>
    /// Cost category.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Benefit classification.
    /// </summary>
    public CodeableConcept? Category { get; set; }

    /// <summary>
    /// In or out of network.
    /// </summary>
    public CodeableConcept? Network { get; set; }

    /// <summary>
    /// Individual or family.
    /// </summary>
    public CodeableConcept? Unit { get; set; }

    /// <summary>
    /// Annual or lifetime.
    /// </summary>
    public CodeableConcept? Term { get; set; }

    /// <summary>
    /// Amount or percentage due from the beneficiary.
    /// </summary>
    public SimpleQuantity? ValueQuantity { get; set; }

    /// <summary>
    /// Amount due from the beneficiary.
    /// </summary>
    public Money? ValueMoney { get; set; }

    /// <summary>
    /// Exceptions for patient payments.
    /// </summary>
    public IList<CoverageCostToBeneficiaryException> Exception { get; set; } = new List<CoverageCostToBeneficiaryException>();
}
