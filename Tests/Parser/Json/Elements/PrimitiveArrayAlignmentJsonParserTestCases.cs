using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class PrimitiveArrayAlignmentJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Elements", "primitive-array-given-alignment.json"),
            "Patient",
            AssertPatientGivenAlignment)
    ];

    private static void AssertPatientGivenAlignment(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-given-alignment", patient.Id, "patient.Id");
        ParserAssert.Count(1, patient.Contact, "patient.Contact");

        var name = ParserAssert.NotNull(patient.Contact[0].Name, "patient.Contact[0].Name");
        ParserAssert.Equal("Lin", ParserAssert.NotNull(name.Family, "patient.Contact[0].Name.Family").Value, "patient.Contact[0].Name.Family.Value");
        ParserAssert.Count(3, name.Given, "patient.Contact[0].Name.Given");

        ParserAssert.Equal("Amy", name.Given[0].Value, "patient.Contact[0].Name.Given[0].Value");
        ParserAssert.Equal(null, name.Given[1].Value, "patient.Contact[0].Name.Given[1].Value");
        ParserAssert.Count(1, name.Given[1].Extension, "patient.Contact[0].Name.Given[1].Extension");

        var givenExtension = name.Given[1].Extension[0];
        ParserAssert.Equal(
            "http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier",
            givenExtension.Url,
            "patient.Contact[0].Name.Given[1].Extension[0].Url");

        var givenExtensionValue = ParserAssert.IsType<FhirCode>(
            givenExtension.Value,
            "patient.Contact[0].Name.Given[1].Extension[0].Value");
        ParserAssert.Equal("MID", givenExtensionValue.Value, "patient.Contact[0].Name.Given[1].Extension[0].Value.Value");

        ParserAssert.Equal("Cara", name.Given[2].Value, "patient.Contact[0].Name.Given[2].Value");
    }
}
