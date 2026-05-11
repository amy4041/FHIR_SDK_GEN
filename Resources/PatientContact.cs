using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Contact party for a FHIR R4 Patient resource.
/// </summary>
public sealed class PatientContact : BackboneElement
{
    /// <summary>
    /// Relationship of the contact to the patient.
    /// </summary>
    public IList<CodeableConcept> Relationship { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Name of the contact party.
    /// </summary>
    public HumanName? Name { get; set; }

    /// <summary>
    /// Contact details for the contact party.
    /// </summary>
    public IList<ContactPoint> Telecom { get; set; } = new List<ContactPoint>();

    /// <summary>
    /// Address of the contact party.
    /// </summary>
    public Address? Address { get; set; }

    /// <summary>
    /// male | female | other | unknown.
    /// </summary>
    public FhirCode? Gender { get; set; }

    /// <summary>
    /// Organization associated with the contact party.
    /// </summary>
    public Reference? Organization { get; set; }

    /// <summary>
    /// Time period when this contact is or was valid.
    /// </summary>
    public Period? Period { get; set; }
}
