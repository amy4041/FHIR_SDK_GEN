using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class MvpResourceJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Resources", "bundle-search-result.json"),
            "Bundle",
            AssertBundleSearchResult),
        new JsonParserTestCase(
            Path.Combine("Resources", "claim-professional.json"),
            "Claim",
            AssertProfessionalClaim),
        new JsonParserTestCase(
            Path.Combine("Resources", "coverage-primary.json"),
            "Coverage",
            AssertPrimaryCoverage),
        new JsonParserTestCase(
            Path.Combine("Resources", "encounter-ambulatory.json"),
            "Encounter",
            AssertAmbulatoryEncounter),
        new JsonParserTestCase(
            Path.Combine("Resources", "organization-clinic.json"),
            "Organization",
            AssertClinicOrganization),
        new JsonParserTestCase(
            Path.Combine("Resources", "practitioner-primary.json"),
            "Practitioner",
            AssertPrimaryPractitioner)
    ];

    private static void AssertBundleSearchResult(FhirJsonParser parser, string json)
    {
        var bundle = parser.Parse<Bundle>(json);

        ParserAssert.Equal("bundle-search-result", bundle.Id, "bundle.Id");
        ParserAssert.Equal("searchset", ParserAssert.NotNull(bundle.Type, "bundle.Type").Value, "bundle.Type.Value");
        ParserAssert.Equal("2026-06-10T03:00:00Z", ParserAssert.NotNull(bundle.Timestamp, "bundle.Timestamp").Value, "bundle.Timestamp.Value");
        ParserAssert.Equal(1, ParserAssert.NotNull(bundle.Total, "bundle.Total").Value, "bundle.Total.Value");

        ParserAssert.Count(1, bundle.Link, "bundle.Link");
        ParserAssert.Equal("self", ParserAssert.NotNull(bundle.Link[0].Relation, "bundle.Link[0].Relation").Value, "bundle.Link[0].Relation.Value");
        ParserAssert.Equal("https://fhir.example.org/Patient?family=Chalmers", ParserAssert.NotNull(bundle.Link[0].Url, "bundle.Link[0].Url").Value, "bundle.Link[0].Url.Value");

        ParserAssert.Count(1, bundle.Entry, "bundle.Entry");
        var entry = bundle.Entry[0];
        ParserAssert.Equal("https://fhir.example.org/Patient/patient-simple", ParserAssert.NotNull(entry.FullUrl, "bundle.Entry[0].FullUrl").Value, "bundle.Entry[0].FullUrl.Value");

        var patient = ParserAssert.IsType<Patient>(entry.Resource, "bundle.Entry[0].Resource");
        ParserAssert.Equal("patient-simple", patient.Id, "bundle.Entry[0].Resource.Id");
        ParserAssert.Equal(true, ParserAssert.NotNull(patient.Active, "bundle.Entry[0].Resource.Active").Value, "bundle.Entry[0].Resource.Active.Value");
        ParserAssert.Count(1, patient.Name, "bundle.Entry[0].Resource.Name");
        ParserAssert.Equal("Chalmers", ParserAssert.NotNull(patient.Name[0].Family, "bundle.Entry[0].Resource.Name[0].Family").Value, "bundle.Entry[0].Resource.Name[0].Family.Value");
        ParserAssert.Count(1, patient.Name[0].Given, "bundle.Entry[0].Resource.Name[0].Given");
        ParserAssert.Equal("Peter", patient.Name[0].Given[0].Value, "bundle.Entry[0].Resource.Name[0].Given[0].Value");

        var search = ParserAssert.NotNull(entry.Search, "bundle.Entry[0].Search");
        ParserAssert.Equal("match", ParserAssert.NotNull(search.Mode, "bundle.Entry[0].Search.Mode").Value, "bundle.Entry[0].Search.Mode.Value");
        ParserAssert.Equal("1.0", ParserAssert.NotNull(search.Score, "bundle.Entry[0].Search.Score").Literal, "bundle.Entry[0].Search.Score.Literal");
    }

    private static void AssertProfessionalClaim(FhirJsonParser parser, string json)
    {
        var claim = parser.Parse<Claim>(json);

        ParserAssert.Equal("claim-professional", claim.Id, "claim.Id");
        ParserAssert.Count(1, claim.Identifier, "claim.Identifier");
        AssertIdentifier(claim.Identifier[0], "http://payer.example.org/claims", "CLM-1001", "claim.Identifier[0]");
        ParserAssert.Equal("active", ParserAssert.NotNull(claim.Status, "claim.Status").Value, "claim.Status.Value");
        AssertConcept(claim.Type, "http://terminology.hl7.org/CodeSystem/claim-type", "professional", "Professional", "claim.Type");
        ParserAssert.Equal("claim", ParserAssert.NotNull(claim.Use, "claim.Use").Value, "claim.Use.Value");
        AssertReference(claim.Patient, "Patient/patient-simple", "Peter Chalmers", "claim.Patient");
        ParserAssert.Equal("2026-06-10", ParserAssert.NotNull(claim.Created, "claim.Created").Value, "claim.Created.Value");
        AssertReference(claim.Insurer, "Organization/acme-payer", "ACME Health Plan", "claim.Insurer");
        AssertReference(claim.Provider, "Practitioner/practitioner-primary", "Dr Alice Ng", "claim.Provider");
        AssertConcept(claim.Priority, "http://terminology.hl7.org/CodeSystem/processpriority", "normal", "Normal", "claim.Priority");

        ParserAssert.Count(1, claim.Insurance, "claim.Insurance");
        var insurance = claim.Insurance[0];
        ParserAssert.Equal(1, ParserAssert.NotNull(insurance.Sequence, "claim.Insurance[0].Sequence").Value, "claim.Insurance[0].Sequence.Value");
        ParserAssert.Equal(true, ParserAssert.NotNull(insurance.Focal, "claim.Insurance[0].Focal").Value, "claim.Insurance[0].Focal.Value");
        AssertReference(insurance.Coverage, "Coverage/coverage-primary", null, "claim.Insurance[0].Coverage");

        ParserAssert.Count(1, claim.Item, "claim.Item");
        var item = claim.Item[0];
        ParserAssert.Equal(1, ParserAssert.NotNull(item.Sequence, "claim.Item[0].Sequence").Value, "claim.Item[0].Sequence.Value");
        AssertConcept(item.ProductOrService, "http://example.org/fhir/CodeSystem/services", "office-visit", "Office visit", "claim.Item[0].ProductOrService", "Office visit");
        ParserAssert.Equal("2026-06-09", ParserAssert.NotNull(item.ServicedDate, "claim.Item[0].ServicedDate").Value, "claim.Item[0].ServicedDate.Value");
        var quantity = ParserAssert.NotNull(item.Quantity, "claim.Item[0].Quantity");
        ParserAssert.Equal("1", ParserAssert.NotNull(quantity.Value, "claim.Item[0].Quantity.Value").Literal, "claim.Item[0].Quantity.Value.Literal");
        ParserAssert.Equal("visit", ParserAssert.NotNull(quantity.Unit, "claim.Item[0].Quantity.Unit").Value, "claim.Item[0].Quantity.Unit.Value");
        ParserAssert.Equal("http://unitsofmeasure.org", ParserAssert.NotNull(quantity.System, "claim.Item[0].Quantity.System").Value, "claim.Item[0].Quantity.System.Value");
        ParserAssert.Equal("{visit}", ParserAssert.NotNull(quantity.Code, "claim.Item[0].Quantity.Code").Value, "claim.Item[0].Quantity.Code.Value");
        AssertMoney(item.UnitPrice, "125.00", "USD", "claim.Item[0].UnitPrice");
        AssertMoney(item.Net, "125.00", "USD", "claim.Item[0].Net");
        AssertMoney(claim.Total, "125.00", "USD", "claim.Total");
    }

    private static void AssertPrimaryCoverage(FhirJsonParser parser, string json)
    {
        var coverage = parser.Parse<Coverage>(json);

        ParserAssert.Equal("coverage-primary", coverage.Id, "coverage.Id");
        ParserAssert.Count(1, coverage.Identifier, "coverage.Identifier");
        AssertIdentifier(coverage.Identifier[0], "http://payer.example.org/member-id", "MB-123", "coverage.Identifier[0]");
        ParserAssert.Equal("active", ParserAssert.NotNull(coverage.Status, "coverage.Status").Value, "coverage.Status.Value");
        ParserAssert.Equal("insurance", ParserAssert.NotNull(coverage.Kind, "coverage.Kind").Value, "coverage.Kind.Value");
        AssertConcept(coverage.Type, "http://terminology.hl7.org/CodeSystem/v3-ActCode", "EHCPOL", "extended healthcare", "coverage.Type");
        ParserAssert.Count(1, coverage.SubscriberId, "coverage.SubscriberId");
        AssertIdentifier(coverage.SubscriberId[0], "http://payer.example.org/subscriber-id", "SUB-456", "coverage.SubscriberId[0]");
        AssertReference(coverage.Beneficiary, "Patient/patient-simple", "Peter Chalmers", "coverage.Beneficiary");
        ParserAssert.Equal("2026-01-01", ParserAssert.NotNull(ParserAssert.NotNull(coverage.Period, "coverage.Period").Start, "coverage.Period.Start").Value, "coverage.Period.Start.Value");
        ParserAssert.Equal("2026-12-31", ParserAssert.NotNull(ParserAssert.NotNull(coverage.Period, "coverage.Period").End, "coverage.Period.End").Value, "coverage.Period.End.Value");
        AssertReference(coverage.Insurer, "Organization/acme-payer", "ACME Health Plan", "coverage.Insurer");

        ParserAssert.Count(1, coverage.Class, "coverage.Class");
        var coverageClass = coverage.Class[0];
        AssertConcept(coverageClass.Type, "http://terminology.hl7.org/CodeSystem/coverage-class", "group", "Group", "coverage.Class[0].Type");
        AssertIdentifier(coverageClass.Value, "http://payer.example.org/groups", "GRP-77", "coverage.Class[0].Value");
        ParserAssert.Equal("Gold Plan", ParserAssert.NotNull(coverageClass.Name, "coverage.Class[0].Name").Value, "coverage.Class[0].Name.Value");
        ParserAssert.Equal(1, ParserAssert.NotNull(coverage.Order, "coverage.Order").Value, "coverage.Order.Value");
        ParserAssert.Equal("preferred", ParserAssert.NotNull(coverage.Network, "coverage.Network").Value, "coverage.Network.Value");
        ParserAssert.Equal(false, ParserAssert.NotNull(coverage.Subrogation, "coverage.Subrogation").Value, "coverage.Subrogation.Value");
    }

    private static void AssertAmbulatoryEncounter(FhirJsonParser parser, string json)
    {
        var encounter = parser.Parse<Encounter>(json);

        ParserAssert.Equal("encounter-ambulatory", encounter.Id, "encounter.Id");
        ParserAssert.Count(1, encounter.Identifier, "encounter.Identifier");
        AssertIdentifier(encounter.Identifier[0], "http://hospital.example.org/encounters", "ENC-20260609-1", "encounter.Identifier[0]");
        ParserAssert.Equal("finished", ParserAssert.NotNull(encounter.Status, "encounter.Status").Value, "encounter.Status.Value");
        ParserAssert.Count(1, encounter.Class, "encounter.Class");
        AssertConcept(encounter.Class[0], "http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB", "ambulatory", "encounter.Class[0]");
        ParserAssert.Count(1, encounter.Type, "encounter.Type");
        AssertConcept(encounter.Type[0], "http://snomed.info/sct", "185349003", "Encounter for check up", "encounter.Type[0]");
        AssertReference(encounter.Subject, "Patient/patient-simple", "Peter Chalmers", "encounter.Subject");
        AssertReference(encounter.ServiceProvider, "Organization/acme-clinic", "ACME Clinic", "encounter.ServiceProvider");

        ParserAssert.Count(1, encounter.Participant, "encounter.Participant");
        var participant = encounter.Participant[0];
        ParserAssert.Count(1, participant.Type, "encounter.Participant[0].Type");
        AssertConcept(participant.Type[0], "http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "PPRF", "primary performer", "encounter.Participant[0].Type[0]");
        AssertReference(participant.Actor, "Practitioner/practitioner-primary", "Dr Alice Ng", "encounter.Participant[0].Actor");
        var actualPeriod = ParserAssert.NotNull(encounter.ActualPeriod, "encounter.ActualPeriod");
        ParserAssert.Equal("2026-06-09T09:00:00Z", ParserAssert.NotNull(actualPeriod.Start, "encounter.ActualPeriod.Start").Value, "encounter.ActualPeriod.Start.Value");
        ParserAssert.Equal("2026-06-09T09:30:00Z", ParserAssert.NotNull(actualPeriod.End, "encounter.ActualPeriod.End").Value, "encounter.ActualPeriod.End.Value");
    }

    private static void AssertClinicOrganization(FhirJsonParser parser, string json)
    {
        var organization = parser.Parse<Organization>(json);

        ParserAssert.Equal("organization-clinic", organization.Id, "organization.Id");
        ParserAssert.Count(1, organization.Identifier, "organization.Identifier");
        AssertIdentifier(organization.Identifier[0], "http://example.org/organizations", "ACME-CLINIC", "organization.Identifier[0]");
        ParserAssert.Equal(true, ParserAssert.NotNull(organization.Active, "organization.Active").Value, "organization.Active.Value");
        ParserAssert.Count(1, organization.Type, "organization.Type");
        AssertConcept(organization.Type[0], "http://terminology.hl7.org/CodeSystem/organization-type", "prov", "Healthcare Provider", "organization.Type[0]", "Healthcare Provider");
        ParserAssert.Equal("ACME Clinic", ParserAssert.NotNull(organization.Name, "organization.Name").Value, "organization.Name.Value");
        ParserAssert.Count(1, organization.Alias, "organization.Alias");
        ParserAssert.Equal("ACME Family Care", organization.Alias[0].Value, "organization.Alias[0].Value");
        AssertReference(organization.PartOf, "Organization/acme-health", "ACME Health Network", "organization.PartOf");
    }

    private static void AssertPrimaryPractitioner(FhirJsonParser parser, string json)
    {
        var practitioner = parser.Parse<Practitioner>(json);

        ParserAssert.Equal("practitioner-primary", practitioner.Id, "practitioner.Id");
        ParserAssert.Count(1, practitioner.Identifier, "practitioner.Identifier");
        AssertIdentifier(practitioner.Identifier[0], "http://example.org/licenses", "MD-12345", "practitioner.Identifier[0]");
        ParserAssert.Equal(true, ParserAssert.NotNull(practitioner.Active, "practitioner.Active").Value, "practitioner.Active.Value");

        ParserAssert.Count(1, practitioner.Name, "practitioner.Name");
        var name = practitioner.Name[0];
        ParserAssert.Equal("official", ParserAssert.NotNull(name.Use, "practitioner.Name[0].Use").Value, "practitioner.Name[0].Use.Value");
        ParserAssert.Equal("Ng", ParserAssert.NotNull(name.Family, "practitioner.Name[0].Family").Value, "practitioner.Name[0].Family.Value");
        ParserAssert.Count(1, name.Given, "practitioner.Name[0].Given");
        ParserAssert.Equal("Alice", name.Given[0].Value, "practitioner.Name[0].Given[0].Value");
        ParserAssert.Count(1, name.Prefix, "practitioner.Name[0].Prefix");
        ParserAssert.Equal("Dr", name.Prefix[0].Value, "practitioner.Name[0].Prefix[0].Value");

        ParserAssert.Count(1, practitioner.Telecom, "practitioner.Telecom");
        ParserAssert.Equal("email", ParserAssert.NotNull(practitioner.Telecom[0].System, "practitioner.Telecom[0].System").Value, "practitioner.Telecom[0].System.Value");
        ParserAssert.Equal("alice.ng@example.org", ParserAssert.NotNull(practitioner.Telecom[0].Value, "practitioner.Telecom[0].Value").Value, "practitioner.Telecom[0].Value.Value");
        ParserAssert.Equal("work", ParserAssert.NotNull(practitioner.Telecom[0].Use, "practitioner.Telecom[0].Use").Value, "practitioner.Telecom[0].Use.Value");
        ParserAssert.Equal("female", ParserAssert.NotNull(practitioner.Gender, "practitioner.Gender").Value, "practitioner.Gender.Value");
        ParserAssert.Equal("1980-04-12", ParserAssert.NotNull(practitioner.BirthDate, "practitioner.BirthDate").Value, "practitioner.BirthDate.Value");

        ParserAssert.Count(1, practitioner.Qualification, "practitioner.Qualification");
        var qualification = practitioner.Qualification[0];
        ParserAssert.Count(1, qualification.Identifier, "practitioner.Qualification[0].Identifier");
        AssertIdentifier(qualification.Identifier[0], "http://example.org/board-certifications", "BC-987", "practitioner.Qualification[0].Identifier[0]");
        AssertConcept(qualification.Code, "http://terminology.hl7.org/CodeSystem/v2-0360", "MD", "Doctor of Medicine", "practitioner.Qualification[0].Code");
        AssertReference(qualification.Issuer, "Organization/acme-board", "ACME Medical Board", "practitioner.Qualification[0].Issuer");
    }

    private static void AssertIdentifier(Identifier? identifier, string system, string value, string path)
    {
        var nonNullIdentifier = ParserAssert.NotNull(identifier, path);

        ParserAssert.Equal(system, ParserAssert.NotNull(nonNullIdentifier.System, $"{path}.System").Value, $"{path}.System.Value");
        ParserAssert.Equal(value, ParserAssert.NotNull(nonNullIdentifier.Value, $"{path}.Value").Value, $"{path}.Value.Value");
    }

    private static void AssertConcept(
        CodeableConcept? concept,
        string system,
        string code,
        string display,
        string path,
        string? text = null)
    {
        var nonNullConcept = ParserAssert.NotNull(concept, path);

        ParserAssert.Count(1, nonNullConcept.Coding, $"{path}.Coding");
        var coding = nonNullConcept.Coding[0];
        ParserAssert.Equal(system, ParserAssert.NotNull(coding.System, $"{path}.Coding[0].System").Value, $"{path}.Coding[0].System.Value");
        ParserAssert.Equal(code, ParserAssert.NotNull(coding.Code, $"{path}.Coding[0].Code").Value, $"{path}.Coding[0].Code.Value");
        ParserAssert.Equal(display, ParserAssert.NotNull(coding.Display, $"{path}.Coding[0].Display").Value, $"{path}.Coding[0].Display.Value");

        if (text is not null)
        {
            ParserAssert.Equal(text, ParserAssert.NotNull(nonNullConcept.Text, $"{path}.Text").Value, $"{path}.Text.Value");
        }
    }

    private static void AssertReference(Reference? reference, string referenceValue, string? display, string path)
    {
        var nonNullReference = ParserAssert.NotNull(reference, path);

        ParserAssert.Equal(referenceValue, ParserAssert.NotNull(nonNullReference.ReferenceValue, $"{path}.Reference").Value, $"{path}.Reference.Value");

        if (display is not null)
        {
            ParserAssert.Equal(display, ParserAssert.NotNull(nonNullReference.Display, $"{path}.Display").Value, $"{path}.Display.Value");
        }
    }

    private static void AssertMoney(Money? money, string value, string currency, string path)
    {
        var nonNullMoney = ParserAssert.NotNull(money, path);

        ParserAssert.Equal(value, ParserAssert.NotNull(nonNullMoney.Value, $"{path}.Value").Literal, $"{path}.Value.Literal");
        ParserAssert.Equal(currency, ParserAssert.NotNull(nonNullMoney.Currency, $"{path}.Currency").Value, $"{path}.Currency.Value");
    }
}
