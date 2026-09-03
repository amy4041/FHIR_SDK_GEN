using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class ExtensionValueNewPrimitiveJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Elements", "extension-value-new-primitives.json"),
            "Patient",
            AssertPatient)
    ];

    private static void AssertPatient(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-new-primitives", patient.Id, "patient.Id");
        ParserAssert.Count(3, patient.Extension, "patient.Extension");
        AssertValue<FhirOid>(patient, 0, "urn:oid:1.2.840.10008");
        AssertValue<FhirTime>(patient, 1, "23:59:60.123456789");
        AssertValue<FhirUuid>(
            patient,
            2,
            "urn:uuid:123e4567-e89b-12d3-a456-426614174000");
    }

    private static void AssertValue<TPrimitive>(
        Patient patient,
        int index,
        string expected)
        where TPrimitive : PrimitiveType<string>
    {
        var value = ParserAssert.IsType<TPrimitive>(
            patient.Extension[index].Value,
            $"patient.Extension[{index}].Value");
        ParserAssert.Equal(
            expected,
            value.Value,
            $"patient.Extension[{index}].Value.Value");
    }
}
