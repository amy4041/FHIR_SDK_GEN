using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class ExtensionValueQuantityJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Elements", "extension-value-quantity.json"),
            "Patient",
            AssertPatientValueQuantityExtension)
    ];

    private static void AssertPatientValueQuantityExtension(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-value-quantity-extension", patient.Id, "patient.Id");
        ParserAssert.Count(1, patient.Extension, "patient.Extension");

        var extension = patient.Extension[0];
        ParserAssert.Equal(
            "http://example.org/fhir/StructureDefinition/body-weight",
            extension.Url,
            "patient.Extension[0].Url");

        var quantity = ParserAssert.IsType<Quantity>(
            extension.Value,
            "patient.Extension[0].Value");

        var value = ParserAssert.NotNull(quantity.Value, "patient.Extension[0].Value.Value");
        ParserAssert.Equal("72.50", value.Literal, "patient.Extension[0].Value.Value.Literal");
        ParserAssert.Equal(72.50m, value.Value, "patient.Extension[0].Value.Value.Value");
        ParserAssert.Equal("kg", ParserAssert.NotNull(quantity.Unit, "patient.Extension[0].Value.Unit").Value, "patient.Extension[0].Value.Unit.Value");
        ParserAssert.Equal(
            "http://unitsofmeasure.org",
            ParserAssert.NotNull(quantity.System, "patient.Extension[0].Value.System").Value,
            "patient.Extension[0].Value.System.Value");
        ParserAssert.Equal("kg", ParserAssert.NotNull(quantity.Code, "patient.Extension[0].Value.Code").Value, "patient.Extension[0].Value.Code.Value");
    }
}
