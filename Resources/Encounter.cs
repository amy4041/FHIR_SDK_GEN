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
    /// Classification such as inpatient, outpatient, ambulatory, or emergency.
    /// </summary>
    public Coding? Class { get; set; }

    /// <summary>
    /// Specific type of encounter.
    /// </summary>
    public IList<CodeableConcept> Type { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Broad service type associated with the encounter.
    /// </summary>
    public CodeableConcept? ServiceType { get; set; }

    /// <summary>
    /// Priority of the encounter.
    /// </summary>
    public CodeableConcept? Priority { get; set; }

    /// <summary>
    /// Patient or group present at the encounter.
    /// </summary>
    public Reference? Subject { get; set; }

    /// <summary>
    /// Participants involved in the encounter.
    /// </summary>
    public IList<EncounterParticipant> Participant { get; set; } = new List<EncounterParticipant>();

    /// <summary>
    /// Time period covered by the encounter.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Quantity of time the encounter lasted.
    /// </summary>
    public Quantity? Length { get; set; }

    /// <summary>
    /// Coded reasons for the encounter.
    /// </summary>
    public IList<CodeableConcept> ReasonCode { get; set; } = new List<CodeableConcept>();

    /// <summary>
    /// Resource references describing reasons for the encounter.
    /// </summary>
    public IList<Reference> ReasonReference { get; set; } = new List<Reference>();

    /// <summary>
    /// Diagnoses relevant to the encounter.
    /// </summary>
    public IList<EncounterDiagnosis> Diagnosis { get; set; } = new List<EncounterDiagnosis>();

    /// <summary>
    /// Organization responsible for the encounter.
    /// </summary>
    public Reference? ServiceProvider { get; set; }

    /// <summary>
    /// Encounter this encounter is part of.
    /// </summary>
    public Reference? PartOf { get; set; }

    /// <summary>
    /// Locations where the encounter takes place.
    /// </summary>
    public IList<EncounterLocation> Location { get; set; } = new List<EncounterLocation>();
}
