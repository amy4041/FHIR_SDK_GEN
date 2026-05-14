internal static class JsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        ..BasicRulesJsonTestCases.All,
        ..PrimitiveJsonTestCases.All,
        ..ElementJsonTestCases.All,
        ..PatientJsonTestCases.All
    ];
}
