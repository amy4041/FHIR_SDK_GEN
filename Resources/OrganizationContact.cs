using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Contact party for a FHIR R4 Organization resource.
/// </summary>
public sealed class OrganizationContact : BackboneElement
{
    /// <summary>
    /// Purpose of this contact.
    /// </summary>
    public CodeableConcept? Purpose { get; set; }

    /// <summary>
    /// Name of the contact party.
    /// </summary>
    public HumanName? Name { get; set; }

    /// <summary>
    /// Contact details for this party.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// Address for this contact party.
    /// </summary>
    public Address? Address { get; set; }
}
