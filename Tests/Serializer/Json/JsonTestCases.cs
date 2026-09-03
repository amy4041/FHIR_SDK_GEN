internal static class JsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        ..BasicRulesJsonTestCases.All,
        ..PrimitiveJsonTestCases.All,
        ..PractitionerPrimitiveJsonTestCases.All,
        ..ElementJsonTestCases.All,
        ..PrimitiveArrayAlignmentJsonTestCases.All,
        ..ExtensionValueNewPrimitiveJsonTestCases.All,
        ..ExtensionValueQuantityJsonTestCases.All,
        ..MvpResourceJsonTestCases.All,
        ..PatientJsonTestCases.All
    ];
}
