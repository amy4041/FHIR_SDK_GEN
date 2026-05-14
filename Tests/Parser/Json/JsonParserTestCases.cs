internal static class JsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        ..BasicRulesJsonParserTestCases.All,
        ..ElementJsonParserTestCases.All,
        ..PrimitiveJsonParserTestCases.All,
        ..PatientJsonParserTestCases.All
    ];
}
