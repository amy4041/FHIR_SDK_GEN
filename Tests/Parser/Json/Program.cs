using MyFhirSdk.Serialization.Json;

return RunAllFixtureTests();

static int RunAllFixtureTests()
{
    var parser = new FhirJsonParser();
    var testCases = JsonParserTestCases.All;
    var failures = new List<string>();

    foreach (var testCase in testCases)
    {
        try
        {
            RunFixtureTest(parser, testCase);
            Console.WriteLine($"PASS {testCase.FixtureFileName}");
        }
        catch (Exception exception)
        {
            failures.Add($"""
                {testCase.FixtureFileName} failed to parse as {testCase.ResourceName}.

                {exception.GetType().Name}: {exception.Message}
                """);
            Console.Error.WriteLine($"FAIL {testCase.FixtureFileName}");
        }
    }

    if (failures.Count == 0)
    {
        Console.WriteLine($"All {testCases.Count} JSON parser fixture tests passed.");
        return 0;
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} JSON parser fixture test(s) failed.");
    Console.Error.WriteLine();
    Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures));
    return 1;
}

static void RunFixtureTest(FhirJsonParser parser, JsonParserTestCase testCase)
{
    var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", testCase.FixtureFileName);
    var json = File.ReadAllText(fixturePath);

    testCase.AssertParsedResource(parser, json);
}
