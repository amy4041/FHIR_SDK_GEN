using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class ElementJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Elements", "element-id-and-extension.json"),
            "Patient",
            AssertElementIdAndExtensionPatient)
    ];

    private static void AssertElementIdAndExtensionPatient(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-element-metadata", patient.Id, "patient.Id");
        ParserAssert.Count(1, patient.Extension, "patient.Extension");

        var patientExtension = patient.Extension[0];
        ParserAssert.Equal(
            "http://example.org/fhir/StructureDefinition/patient-note",
            patientExtension.Url,
            "patient.Extension[0].Url");

        var patientExtensionValue = ParserAssert.IsType<FhirString>(
            patientExtension.Value,
            "patient.Extension[0].Value");
        ParserAssert.Equal("front desk", patientExtensionValue.Value, "patient.Extension[0].Value.Value");

        ParserAssert.Count(1, patient.Name, "patient.Name");
        var name = patient.Name[0];
        ParserAssert.Equal("name-element", name.Id, "patient.Name[0].Id");
        ParserAssert.Equal("Lin", ParserAssert.NotNull(name.Family, "patient.Name[0].Family").Value, "patient.Name[0].Family.Value");
        ParserAssert.Count(1, name.Extension, "patient.Name[0].Extension");

        var nameExtension = name.Extension[0];
        ParserAssert.Equal(
            "http://example.org/fhir/StructureDefinition/name-source",
            nameExtension.Url,
            "patient.Name[0].Extension[0].Url");

        var nameExtensionValue = ParserAssert.IsType<FhirString>(
            nameExtension.Value,
            "patient.Name[0].Extension[0].Value");
        ParserAssert.Equal("passport", nameExtensionValue.Value, "patient.Name[0].Extension[0].Value.Value");
    }
}
