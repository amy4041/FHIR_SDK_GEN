namespace MyFhirSdk.Tests.Validation.Rules;

public sealed class RequiredFieldRuleTests
{
    [Fact]
    public void ValidateReportsMissingBundleType()
    {
        var result = new FhirValidator().Validate(new Bundle());

        ValidationAssert.HasIssue(result, "Bundle.type", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingCoverageTopLevelFields()
    {
        var result = new FhirValidator().Validate(new Coverage());

        ValidationAssert.HasIssue(result, "Coverage.status", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Coverage.kind", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Coverage.beneficiary", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingEncounterStatus()
    {
        var result = new FhirValidator().Validate(new Encounter());

        ValidationAssert.HasIssue(result, "Encounter.status", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimTopLevelFields()
    {
        var result = new FhirValidator().Validate(new Claim());

        ValidationAssert.HasIssue(result, "Claim.status", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.type", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.use", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.patient", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.created", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateDoesNotRequirePatientOptionalFields()
    {
        var result = new FhirValidator().Validate(new Patient());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateDoesNotRequireOrganizationOptionalFields()
    {
        var result = new FhirValidator().Validate(new Organization());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateDoesNotRequirePractitionerOptionalFields()
    {
        var result = new FhirValidator().Validate(new Practitioner());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateReportsMissingBundleLinkFields()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Link = [new BundleLink()]
        };

        var result = new FhirValidator().Validate(bundle);

        ValidationAssert.HasIssue(result, "Bundle.link[0].relation", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Bundle.link[0].url", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingCoverageClassFields()
    {
        var coverage = CreateValidCoverage();
        coverage.Class = [new CoverageClass()];

        var result = new FhirValidator().Validate(coverage);

        ValidationAssert.HasIssue(result, "Coverage.class[0].type", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Coverage.class[0].value", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingCoveragePaymentByParty()
    {
        var coverage = CreateValidCoverage();
        coverage.PaymentBy = [new CoveragePaymentBy()];

        var result = new FhirValidator().Validate(coverage);

        ValidationAssert.HasIssue(result, "Coverage.paymentBy[0].party", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingEncounterLocation()
    {
        var encounter = new Encounter
        {
            Status = new FhirCode("in-progress"),
            Location = [new EncounterLocation()]
        };

        var result = new FhirValidator().Validate(encounter);

        ValidationAssert.HasIssue(result, "Encounter.location[0].location", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimPayeeType()
    {
        var claim = CreateValidClaim();
        claim.Payee = new ClaimPayee();

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.payee.type", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimEventType()
    {
        var claim = CreateValidClaim();
        claim.Event = [new ClaimEvent { WhenDateTime = new FhirDateTime("2026-06-17T10:30:00Z") }];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.event[0].type", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimCareTeamFields()
    {
        var claim = CreateValidClaim();
        claim.CareTeam = [new ClaimCareTeam()];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.careTeam[0].sequence", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.careTeam[0].provider", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimSupportingInfoFields()
    {
        var claim = CreateValidClaim();
        claim.SupportingInfo = [new ClaimSupportingInfo()];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.supportingInfo[0].sequence", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.supportingInfo[0].category", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimDiagnosisSequence()
    {
        var claim = CreateValidClaim();
        claim.Diagnosis =
        [
            new ClaimDiagnosis
            {
                DiagnosisCodeableConcept = new CodeableConcept()
            }
        ];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.diagnosis[0].sequence", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimProcedureSequence()
    {
        var claim = CreateValidClaim();
        claim.Procedure =
        [
            new ClaimProcedure
            {
                ProcedureCodeableConcept = new CodeableConcept()
            }
        ];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.procedure[0].sequence", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimInsuranceFields()
    {
        var claim = CreateValidClaim();
        claim.Insurance = [new ClaimInsurance()];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.insurance[0].sequence", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.insurance[0].focal", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.insurance[0].coverage", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimAccidentDate()
    {
        var claim = CreateValidClaim();
        claim.Accident = new ClaimAccident();

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.accident.date", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimItemSequence()
    {
        var claim = CreateValidClaim();
        claim.Item = [new ClaimItem()];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.item[0].sequence", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingClaimDetailSequences()
    {
        var claim = CreateValidClaim();
        claim.Item =
        [
            new ClaimItem
            {
                Sequence = new FhirPositiveInt(1),
                Detail =
                [
                    new ClaimDetail
                    {
                        SubDetail = [new ClaimSubDetail()]
                    }
                ]
            }
        ];

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.item[0].detail[0].sequence", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Claim.item[0].detail[0].subDetail[0].sequence", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingBundleEntryRequestFields()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("batch"),
            Entry = [new BundleEntry { Request = new BundleEntryRequest() }]
        };

        var result = new FhirValidator().Validate(bundle);

        ValidationAssert.HasIssue(result, "Bundle.entry[0].request.method", ValidationIssueCode.Required);
        ValidationAssert.HasIssue(result, "Bundle.entry[0].request.url", ValidationIssueCode.Required);
    }

    [Fact]
    public void ValidateReportsMissingBundleEntryResponseStatus()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("batch-response"),
            Entry = [new BundleEntry { Response = new BundleEntryResponse() }]
        };

        var result = new FhirValidator().Validate(bundle);

        ValidationAssert.HasIssue(result, "Bundle.entry[0].response.status", ValidationIssueCode.Required);
    }

    private static Coverage CreateValidCoverage()
    {
        return new Coverage
        {
            Status = new FhirCode("active"),
            Kind = new FhirCode("insurance"),
            Beneficiary = new Reference { ReferenceValue = new FhirString("Patient/123") }
        };
    }

    private static Claim CreateValidClaim()
    {
        return new Claim
        {
            Status = new FhirCode("active"),
            Type = new CodeableConcept(),
            Use = new FhirCode("claim"),
            Patient = new Reference { ReferenceValue = new FhirString("Patient/123") },
            Created = new FhirDateTime("2026-06-17T10:30:00Z")
        };
    }
}
