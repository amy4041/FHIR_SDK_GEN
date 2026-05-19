using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class PractitionerPrimitiveJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Primitives", "practitioner-integer64-decimal-literal.json"),
            "Practitioner",
            AssertPractitionerPhotoPrimitives)
    ];

    private static void AssertPractitionerPhotoPrimitives(FhirJsonParser parser, string json)
    {
        var practitioner = parser.Parse<Practitioner>(json);

        ParserAssert.Equal("practitioner-integer64-decimal-literal", practitioner.Id, "practitioner.Id");
        ParserAssert.Count(1, practitioner.Photo, "practitioner.Photo");

        var photo = practitioner.Photo[0];
        ParserAssert.Equal(
            "image/jpeg",
            ParserAssert.NotNull(photo.ContentType, "practitioner.Photo[0].ContentType").Value,
            "practitioner.Photo[0].ContentType.Value");

        var size = ParserAssert.NotNull(photo.Size, "practitioner.Photo[0].Size");
        ParserAssert.Equal("1048576", size.Literal, "practitioner.Photo[0].Size.Literal");
        ParserAssert.Equal(1048576L, size.Value, "practitioner.Photo[0].Size.Value");

        var duration = ParserAssert.NotNull(photo.Duration, "practitioner.Photo[0].Duration");
        ParserAssert.Equal("2.50", duration.Literal, "practitioner.Photo[0].Duration.Literal");
        ParserAssert.Equal(2.50m, duration.Value, "practitioner.Photo[0].Duration.Value");
    }
}
