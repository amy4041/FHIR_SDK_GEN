using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class PrimitiveJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Primitives", "primitive-values-and-metadata.json"),
            "Patient",
            AssertPrimitiveValuesAndMetadataPatient)
    ];

    private static void AssertPrimitiveValuesAndMetadataPatient(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-primitive-metadata", patient.Id, "patient.Id");

        var active = ParserAssert.NotNull(patient.Active, "patient.Active");
        ParserAssert.Equal(true, active.Value, "patient.Active.Value");
        ParserAssert.Equal("active-element", active.Id, "patient.Active.Id");
        ParserAssert.Count(1, active.Extension, "patient.Active.Extension");

        var activeExtension = active.Extension[0];
        ParserAssert.Equal(
            "http://example.org/fhir/StructureDefinition/source",
            activeExtension.Url,
            "patient.Active.Extension[0].Url");

        var activeExtensionValue = ParserAssert.IsType<FhirString>(
            activeExtension.Value,
            "patient.Active.Extension[0].Value");
        ParserAssert.Equal(
            "registration",
            activeExtensionValue.Value,
            "patient.Active.Extension[0].Value.Value");

        var gender = ParserAssert.NotNull(patient.Gender, "patient.Gender");
        ParserAssert.Equal("male", gender.Value, "patient.Gender.Value");

        var birthDate = ParserAssert.NotNull(patient.BirthDate, "patient.BirthDate");
        ParserAssert.Equal("1974-12-25", birthDate.Value, "patient.BirthDate.Value");
    }
}
