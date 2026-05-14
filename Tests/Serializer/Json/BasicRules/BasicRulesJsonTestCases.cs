using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class BasicRulesJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("BasicRules", "resource-type-camel-case-and-empty-values.json"),
            "Patient",
            CreateResourceTypeCamelCaseAndEmptyValuesPatient)
    ];

    private static Resource CreateResourceTypeCamelCaseAndEmptyValuesPatient()
    {
        return new Patient
        {
            Id = "patient-basic-rules",
            ImplicitRules = "http://example.org/fhir/rules",
            Language = "en-US",
            DeceasedBoolean = new FhirBoolean(false),
            MultipleBirthInteger = new FhirInteger(2)
        };
    }
}
