using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class BasicRulesJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("BasicRules", "resource-type-camel-case-and-empty-values.json"),
            "Patient",
            AssertResourceTypeCamelCaseAndEmptyValuesPatient)
    ];

    private static void AssertResourceTypeCamelCaseAndEmptyValuesPatient(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("Patient", patient.ResourceType, "patient.ResourceType");
        ParserAssert.Equal("patient-basic-rules", patient.Id, "patient.Id");
        ParserAssert.Equal("http://example.org/fhir/rules", patient.ImplicitRules, "patient.ImplicitRules");
        ParserAssert.Equal("en-US", patient.Language, "patient.Language");

        var deceasedBoolean = ParserAssert.NotNull(patient.DeceasedBoolean, "patient.DeceasedBoolean");
        ParserAssert.Equal(false, deceasedBoolean.Value, "patient.DeceasedBoolean.Value");

        var multipleBirthInteger = ParserAssert.NotNull(patient.MultipleBirthInteger, "patient.MultipleBirthInteger");
        ParserAssert.Equal(2, multipleBirthInteger.Value, "patient.MultipleBirthInteger.Value");

        ParserAssert.Count(0, patient.Name, "patient.Name");
        ParserAssert.Count(0, patient.Telecom, "patient.Telecom");
        ParserAssert.Count(0, patient.Address, "patient.Address");
    }
}
