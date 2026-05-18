using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// Care team member for a FHIR R5 Claim.
/// </summary>
public sealed class ClaimCareTeam : BackboneElement
{
    /// <summary>
    /// Care team sequence number.
    /// </summary>
    public FhirPositiveInt? Sequence { get; set; }

    /// <summary>
    /// Practitioner or organization.
    /// </summary>
    public Reference? Provider { get; set; }

    /// <summary>
    /// Indicator of the lead practitioner.
    /// </summary>
    public FhirBoolean? Responsible { get; set; }

    /// <summary>
    /// Function within the team.
    /// </summary>
    public CodeableConcept? Role { get; set; }

    /// <summary>
    /// Practitioner or provider specialization.
    /// </summary>
    public CodeableConcept? Specialty { get; set; }
}
