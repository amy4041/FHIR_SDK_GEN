public sealed class FhirJsonParserFixtureTests
{
    public static IEnumerable<object[]> FixtureCases
    {
        get
        {
            return JsonParserTestCases.All.Select(testCase => new object[] { testCase });
        }
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void ParseMatchesFixture(JsonParserTestCase testCase)
    {
        var parser = new FhirJsonParser();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", testCase.FixtureFileName);
        var json = File.ReadAllText(fixturePath);

        testCase.AssertParsedResource(parser, json);
    }
}
