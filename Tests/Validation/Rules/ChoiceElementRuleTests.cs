namespace MyFhirSdk.Tests.Validation.Rules;

public static class ChoiceElementRuleTests
{
    public static void ValidateReportsPatientDeceasedChoiceConflict()
    {
        var patient = new Patient
        {
            DeceasedBoolean = new FhirBoolean(false),
            DeceasedDateTime = new FhirDateTime("2026-06-05")
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.deceased[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsPatientMultipleBirthChoiceConflict()
    {
        var patient = new Patient
        {
            MultipleBirthBoolean = new FhirBoolean(true),
            MultipleBirthInteger = new FhirInteger(1)
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.multipleBirth[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsPractitionerDeceasedChoiceConflict()
    {
        var practitioner = new Practitioner
        {
            DeceasedBoolean = new FhirBoolean(false),
            DeceasedDateTime = new FhirDateTime("2026-06-05")
        };

        var result = new FhirValidator().Validate(practitioner);

        TestAssert.HasIssue(result, "Practitioner.deceased[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimEventWhenChoiceMissingOrConflicting()
    {
        var missingChoiceClaim = CreateValidClaim();
        missingChoiceClaim.Event =
        [
            new ClaimEvent
            {
                Type = new CodeableConcept()
            }
        ];

        var missingChoiceResult = new FhirValidator().Validate(missingChoiceClaim);

        TestAssert.HasIssue(missingChoiceResult, "Claim.event[0].when[x]", ValidationIssueCode.ChoiceElement);

        var conflictingChoiceClaim = CreateValidClaim();
        conflictingChoiceClaim.Event =
        [
            new ClaimEvent
            {
                Type = new CodeableConcept(),
                WhenDateTime = new FhirDateTime("2026-06-17T10:30:00Z"),
                WhenPeriod = new Period()
            }
        ];

        var conflictingChoiceResult = new FhirValidator().Validate(conflictingChoiceClaim);

        TestAssert.HasIssue(conflictingChoiceResult, "Claim.event[0].when[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimSupportingInfoTimingChoiceConflict()
    {
        var claim = CreateValidClaim();
        claim.SupportingInfo =
        [
            new ClaimSupportingInfo
            {
                Sequence = new FhirPositiveInt(1),
                Category = new CodeableConcept(),
                TimingDate = new FhirDate("2026-06-17"),
                TimingPeriod = new Period()
            }
        ];

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.supportingInfo[0].timing[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimSupportingInfoValueChoiceConflict()
    {
        var claim = CreateValidClaim();
        claim.SupportingInfo =
        [
            new ClaimSupportingInfo
            {
                Sequence = new FhirPositiveInt(1),
                Category = new CodeableConcept(),
                ValueBoolean = new FhirBoolean(true),
                ValueString = new FhirString("supporting value")
            }
        ];

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.supportingInfo[0].value[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimDiagnosisChoiceMissingOrConflicting()
    {
        var missingChoiceClaim = CreateValidClaim();
        missingChoiceClaim.Diagnosis =
        [
            new ClaimDiagnosis
            {
                Sequence = new FhirPositiveInt(1)
            }
        ];

        var missingChoiceResult = new FhirValidator().Validate(missingChoiceClaim);

        TestAssert.HasIssue(missingChoiceResult, "Claim.diagnosis[0].diagnosis[x]", ValidationIssueCode.ChoiceElement);

        var conflictingChoiceClaim = CreateValidClaim();
        conflictingChoiceClaim.Diagnosis =
        [
            new ClaimDiagnosis
            {
                Sequence = new FhirPositiveInt(1),
                DiagnosisCodeableConcept = new CodeableConcept(),
                DiagnosisReference = new Reference { ReferenceValue = new FhirString("Condition/123") }
            }
        ];

        var conflictingChoiceResult = new FhirValidator().Validate(conflictingChoiceClaim);

        TestAssert.HasIssue(conflictingChoiceResult, "Claim.diagnosis[0].diagnosis[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimProcedureChoiceMissingOrConflicting()
    {
        var missingChoiceClaim = CreateValidClaim();
        missingChoiceClaim.Procedure =
        [
            new ClaimProcedure
            {
                Sequence = new FhirPositiveInt(1)
            }
        ];

        var missingChoiceResult = new FhirValidator().Validate(missingChoiceClaim);

        TestAssert.HasIssue(missingChoiceResult, "Claim.procedure[0].procedure[x]", ValidationIssueCode.ChoiceElement);

        var conflictingChoiceClaim = CreateValidClaim();
        conflictingChoiceClaim.Procedure =
        [
            new ClaimProcedure
            {
                Sequence = new FhirPositiveInt(1),
                ProcedureCodeableConcept = new CodeableConcept(),
                ProcedureReference = new Reference { ReferenceValue = new FhirString("Procedure/123") }
            }
        ];

        var conflictingChoiceResult = new FhirValidator().Validate(conflictingChoiceClaim);

        TestAssert.HasIssue(conflictingChoiceResult, "Claim.procedure[0].procedure[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimAccidentLocationChoiceConflict()
    {
        var claim = CreateValidClaim();
        claim.Accident = new ClaimAccident
        {
            Date = new FhirDate("2026-06-17"),
            LocationAddress = new Address(),
            LocationReference = new Reference { ReferenceValue = new FhirString("Location/123") }
        };

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.accident.location[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimItemServicedChoiceConflict()
    {
        var claim = CreateValidClaim();
        claim.Item =
        [
            new ClaimItem
            {
                Sequence = new FhirPositiveInt(1),
                ServicedDate = new FhirDate("2026-06-17"),
                ServicedPeriod = new Period()
            }
        ];

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.item[0].serviced[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsClaimItemLocationChoiceConflict()
    {
        var claim = CreateValidClaim();
        claim.Item =
        [
            new ClaimItem
            {
                Sequence = new FhirPositiveInt(1),
                LocationCodeableConcept = new CodeableConcept(),
                LocationAddress = new Address(),
                LocationReference = new Reference { ReferenceValue = new FhirString("Location/123") }
            }
        ];

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.item[0].location[x]", ValidationIssueCode.ChoiceElement);
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
