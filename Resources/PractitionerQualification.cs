using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Qualification, certification, or license for a FHIR R5 Practitioner.
/// </summary>
public sealed class PractitionerQualification : BackboneElement
{
    /// <summary>
    /// Identifiers for the qualification.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// Coded representation of the qualification.
    /// </summary>
    public CodeableConcept? Code { get; set; }

    /// <summary>
    /// Time period when the qualification is valid.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Organization that issued the qualification.
    /// </summary>
    public Reference? Issuer { get; set; }
}
