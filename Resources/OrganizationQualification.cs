using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Qualification, certification, accreditation, license, or training for a FHIR R5 Organization.
/// </summary>
public sealed class OrganizationQualification : BackboneElement
{
    /// <summary>
    /// Identifiers for this qualification.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Coded representation of the qualification.
    /// </summary>
    public CodeableConcept? Code { get; set; }

    /// <summary>
    /// Period during which the qualification is valid.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Organization that regulates and issues this qualification.
    /// </summary>
    public Reference? Issuer { get; set; }
}
