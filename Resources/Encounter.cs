using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Encounter resource for an interaction between a patient and care providers.
/// </summary>
public sealed class Encounter : DomainResource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Encounter";

    /// <summary>
    /// Identifiers assigned to this encounter.
    /// </summary>
    public IList<Identifier> Identifier { get; set; } = new List<Identifier>();

    /// <summary>
    /// planned | arrived | triaged | in-progress | onleave | finished | cancelled | entered-in-error | unknown.
    /// </summary>
    public FhirCode? Status { get; set; }

    /// <summary>
    /// Classifications such as inpatient, outpatient, ambulatory, or emergency.
    /// </summary>
    public IList<CodeableConcept> Class { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Priority of the encounter.
    /// </summary>
    public CodeableConcept? Priority { get; set; }

    /// <summary>
    /// Specific type of encounter.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Broad service type associated with the encounter.
    /// </summary>
    public IList<CodeableReference> ServiceType { get; set; } = new List<CodeableReference>();

    /// <summary>
    /// Patient or group present at the encounter.
    /// </summary>
    public Reference? Subject { get; set; }

    /// <summary>
    /// Current status of the subject in relation to the encounter.
    /// </summary>
    public CodeableConcept? SubjectStatus { get; set; }

    /// <summary>
    /// Episodes of care that this encounter should be recorded against.
    /// </summary>
    public IList<Reference> EpisodeOfCare { get; set; } = new List<Reference>();

    /// <summary>
    /// Requests that initiated this encounter.
    /// </summary>
    public IList<Reference> BasedOn { get; set; } = new List<Reference>();

    /// <summary>
    /// Care teams allocated to participate in this encounter.
    /// </summary>
    public IList<Reference> CareTeam { get; set; } = new List<Reference>();

    /// <summary>
    /// Encounter this encounter is part of.
    /// </summary>
    public Reference? PartOf { get; set; }

    /// <summary>
    /// Organization responsible for the encounter.
    /// </summary>
    public Reference? ServiceProvider { get; set; }

    /// <summary>
    /// Participants involved in the encounter.
    /// </summary>
    public IList<EncounterParticipant> Participant { get; set; } = new List<EncounterParticipant>();

    /// <summary>
    /// Appointments that scheduled this encounter.
    /// </summary>
    public IList<Reference> Appointment { get; set; } = new List<Reference>();

    /// <summary>
    /// Connection details of virtual services.
    /// </summary>
    public IList<VirtualServiceDetail> VirtualService { get; set; } = new List<VirtualServiceDetail>();

    /// <summary>
    /// Actual start and end time of the encounter.
    /// </summary>
    public Period? ActualPeriod { get; set; }

    /// <summary>
    /// Planned start date/time of the encounter.
    /// </summary>
    public FhirDateTime? PlannedStartDate { get; set; }

    /// <summary>
    /// Planned end date/time of the encounter.
    /// </summary>
    public FhirDateTime? PlannedEndDate { get; set; }

    /// <summary>
    /// Quantity of time the encounter lasted.
    /// </summary>
    public Duration? Length { get; set; }

    /// <summary>
    /// Medical reasons expected to be addressed during the encounter.
    /// </summary>
    public IList<EncounterReason> Reason { get; set; } = new List<EncounterReason>();

    /// <summary>
    /// Diagnoses relevant to the encounter.
    /// </summary>
    public IList<EncounterDiagnosis> Diagnosis { get; set; } = new List<EncounterDiagnosis>();

    /// <summary>
    /// Accounts that may be used for billing this encounter.
    /// </summary>
    public IList<Reference> Account { get; set; } = new List<Reference>();

    /// <summary>
    /// Diet preferences reported by the patient.
    /// </summary>
    public IList<CodeableConcept> DietPreference { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Special arrangements such as wheelchair, translator, or stretcher.
    /// </summary>
    public IList<CodeableConcept> SpecialArrangement { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Special courtesies such as VIP or board member.
    /// </summary>
    public IList<CodeableConcept> SpecialCourtesy { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Details about the admission to a healthcare service.
    /// </summary>
    public EncounterAdmission? Admission { get; set; }

    /// <summary>
    /// Locations where the encounter takes place.
    /// </summary>
    public IList<EncounterLocation> Location { get; set; } = new List<EncounterLocation>();
}
