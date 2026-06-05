using MyFhirSdk.Tests.Validation.Rules;
using MyFhirSdk.Tests.Validation.Traversal;

namespace MyFhirSdk.Tests.Validation;

public static class Program
{
    public static async Task<int> Main()
    {
        var tests = new List<(string Name, Func<Task> Run)>
        {
            Test("ValidationResult empty issues is valid", ValidationResultTests.EmptyIssuesIsValid),
            Test("ValidationResult with issues is invalid", ValidationResultTests.IssuesMakeResultInvalid),
            Test("ValidationResult preserves issue details", ValidationResultTests.PreservesIssueDetails),
            Test("FhirValidator rejects null resource", FhirValidatorTests.ValidateRejectsNullResource),
            Test("FhirValidator returns success for empty optional Patient", FhirValidatorTests.ValidateReturnsSuccessForEmptyOptionalPatient),
            Test("Traversal reports indexed nested primitive path", FhirObjectGraphWalkerTests.ValidateReportsIndexedPathForNestedPrimitive),
            Test("Traversal ignores null optional fields", FhirObjectGraphWalkerTests.ValidateIgnoresNullOptionalFields),
            Test("Path formatter uses JsonPropertyName attribute", FhirPathFormatterTests.ValidateUsesJsonPropertyNameAttributeInIssuePath),
            Test("PrimitiveFormat reports invalid resource id", PrimitiveFormatRuleTests.ValidateReportsInvalidResourceId),
            Test("PrimitiveFormat reports invalid FhirDate", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirDate),
            Test("PrimitiveFormat reports invalid FhirCode", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirCode),
            Test("PrimitiveFormat reports invalid FhirUri", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirUri),
            Test("PrimitiveFormat reports invalid FhirMarkdown", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirMarkdown),
            Test("PrimitiveFormat reports invalid FhirUrl", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirUrl),
            Test("PrimitiveFormat reports invalid FhirCanonical", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirCanonical),
            Test("PrimitiveFormat reports invalid FhirId", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirId),
            Test("PrimitiveFormat reports invalid FhirDateTime", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirDateTime),
            Test("PrimitiveFormat reports invalid FhirInstant", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirInstant),
            Test("PrimitiveFormat reports invalid FhirDecimal", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirDecimal),
            Test("PrimitiveFormat reports invalid FhirInteger64", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirInteger64),
            Test("PrimitiveFormat reports invalid positiveInt", PrimitiveFormatRuleTests.ValidateReportsInvalidPositiveInt),
            Test("PrimitiveFormat reports invalid unsignedInt", PrimitiveFormatRuleTests.ValidateReportsInvalidUnsignedInt),
            Test("PrimitiveFormat reports invalid FhirBase64Binary", PrimitiveFormatRuleTests.ValidateReportsInvalidFhirBase64Binary),
            Test("PrimitiveFormat accepts valid FhirBoolean and FhirInteger", PrimitiveFormatRuleTests.ValidateDoesNotReportIssueForValidBooleanAndInteger),
            Test("RequiredField reports missing Bundle type", RequiredFieldRuleTests.ValidateReportsMissingBundleType),
            Test("RequiredField reports missing Coverage top-level fields", RequiredFieldRuleTests.ValidateReportsMissingCoverageTopLevelFields),
            Test("RequiredField reports missing Encounter status", RequiredFieldRuleTests.ValidateReportsMissingEncounterStatus),
            Test("RequiredField reports missing Claim top-level fields", RequiredFieldRuleTests.ValidateReportsMissingClaimTopLevelFields),
            Test("RequiredField does not require Patient top-level optional fields", RequiredFieldRuleTests.ValidateDoesNotRequirePatientOptionalFields),
            Test("RequiredField does not require Organization top-level optional fields", RequiredFieldRuleTests.ValidateDoesNotRequireOrganizationOptionalFields),
            Test("RequiredField does not require Practitioner top-level optional fields", RequiredFieldRuleTests.ValidateDoesNotRequirePractitionerOptionalFields),
            Test("RequiredField reports missing Bundle link fields", RequiredFieldRuleTests.ValidateReportsMissingBundleLinkFields),
            Test("RequiredField reports missing Coverage class fields", RequiredFieldRuleTests.ValidateReportsMissingCoverageClassFields),
            Test("RequiredField reports missing Coverage paymentBy party", RequiredFieldRuleTests.ValidateReportsMissingCoveragePaymentByParty),
            Test("RequiredField reports missing Encounter location", RequiredFieldRuleTests.ValidateReportsMissingEncounterLocation),
            Test("Cardinality reports null repeated field", CardinalityRuleTests.ValidateReportsNullRepeatedField),
            Test("Cardinality reports null repeated item", CardinalityRuleTests.ValidateReportsNullRepeatedItem),
            Test("Cardinality reports empty required repeated field", CardinalityRuleTests.ValidateReportsEmptyRequiredRepeatedField),
            Test("ChoiceElement reports conflicting Patient deceased choices", ChoiceElementRuleTests.ValidateReportsPatientDeceasedChoiceConflict),
            Test("ChoiceElement reports conflicting Patient multipleBirth choices", ChoiceElementRuleTests.ValidateReportsPatientMultipleBirthChoiceConflict),
            Test("ChoiceElement reports conflicting Practitioner deceased choices", ChoiceElementRuleTests.ValidateReportsPractitionerDeceasedChoiceConflict)
        };

        var failures = new List<string>();

        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"""
                    {test.Name}

                    {exception.GetType().Name}: {exception.Message}
                    {exception.StackTrace}
                    """);
                Console.Error.WriteLine($"FAIL {test.Name}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine($"All {tests.Count} Validation tests passed.");
            return 0;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine($"{failures.Count} Validation test(s) failed.");
        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures));
        return 1;
    }

    private static (string Name, Func<Task> Run) Test(string name, Action run)
    {
        return (name, () =>
        {
            run();
            return Task.CompletedTask;
        });
    }
}

internal static class TestAssert
{
    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected '{expected}', but got '{actual}'.");
        }
    }

    public static void IsTrue(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be true.");
        }
    }

    public static void IsFalse(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be false.");
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name}, but got {exception.GetType().Name}.",
                exception);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    public static void HasIssue(
        ValidationResult result,
        string path,
        ValidationIssueCode code,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (!result.Issues.Any(issue =>
                issue.Path == path &&
                issue.Code == code &&
                issue.Severity == severity))
        {
            var actualIssues = string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.Code}/{issue.Severity}/{issue.Path}: {issue.Message}"));

            throw new InvalidOperationException($"""
                Expected issue {code}/{severity}/{path}, but it was not found.
                Actual issues:
                {actualIssues}
                """);
        }
    }
}
