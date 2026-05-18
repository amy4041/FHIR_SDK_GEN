using System.Collections.Generic;
using MyFhirSdk.Core;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 ExtendedContactDetail metadata datatype for rich contact information.
/// </summary>
public sealed class ExtendedContactDetail : DataType
{
    /// <summary>
    /// The purpose or type of contact.
    /// </summary>
    public CodeableConcept? Purpose { get; set; }

    /// <summary>
    /// Names of individuals to contact.
    /// </summary>
    public IList<HumanName> Name { get; set; } = new List<HumanName>();

    /// <summary>
    /// Contact details such as phone, fax, URL, or email.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// Address for the contact.
    /// </summary>
    public Address? Address { get; set; }

    /// <summary>
    /// Organization that handles or monitors this contact detail.
    /// </summary>
    public Reference? Organization { get; set; }

    /// <summary>
    /// Period when this contact detail was valid for usage.
    /// </summary>
    public Period? Period { get; set; }
}
