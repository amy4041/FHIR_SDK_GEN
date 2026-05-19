internal static class JsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        ..BasicRulesJsonParserTestCases.All,
        ..ElementJsonParserTestCases.All,
        ..PrimitiveArrayAlignmentJsonParserTestCases.All,
        ..ExtensionValueQuantityJsonParserTestCases.All,
        ..PrimitiveJsonParserTestCases.All,
        ..PractitionerPrimitiveJsonParserTestCases.All,
        ..PatientJsonParserTestCases.All
    ];
}
