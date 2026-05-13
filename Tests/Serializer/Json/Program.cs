using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.Serialization.Json;

return RunAllFixtureTests();

static int RunAllFixtureTests()
{
    var serializer = new FhirJsonSerializer();
    var failures = new List<string>();

    foreach (var testCase in PatientJsonTestCases.All)
    {
        var result = RunFixtureTest(serializer, testCase);

        if (result.Passed)
        {
            Console.WriteLine($"PASS {testCase.FixtureFileName}");
            continue;
        }

        failures.Add(result.Message);
        Console.Error.WriteLine($"FAIL {testCase.FixtureFileName}");
    }

    if (failures.Count == 0)
    {
        Console.WriteLine($"All {PatientJsonTestCases.All.Count} JSON serializer fixture tests passed.");
        return 0;
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} JSON serializer fixture test(s) failed.");
    Console.Error.WriteLine();
    Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures));
    return 1;
}

static JsonFixtureTestResult RunFixtureTest(FhirJsonSerializer serializer, JsonFixtureTestCase testCase)
{
    var actualJson = serializer.Serialize(testCase.CreateResource());
    var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", testCase.FixtureFileName);
    var expectedJson = File.ReadAllText(fixturePath);

    var normalizedExpected = NormalizeJson(expectedJson);
    var normalizedActual = NormalizeJson(actualJson);
    Console.WriteLine("------normalizedExpected------");
    Console.WriteLine(normalizedExpected);
    Console.WriteLine("------normalizedActual------");
    Console.WriteLine(normalizedActual);

    if (normalizedExpected == normalizedActual)
    {
        return JsonFixtureTestResult.Pass();
    }

    return JsonFixtureTestResult.Fail($"""
        {testCase.FixtureFileName} does not match serialized {testCase.ResourceName} resource.

        Expected:
        {normalizedExpected}

        Actual:
        {normalizedActual}
        """);
}

static string NormalizeJson(string json)
{
    var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON payload was empty.");
    var normalized = NormalizeNode(node);

    return normalized.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true
    });
}

static JsonNode NormalizeNode(JsonNode node)
{
    if (node is JsonObject jsonObject)
    {
        var normalizedObject = new JsonObject();

        foreach (var property in jsonObject.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            normalizedObject[property.Key] = property.Value is null
                ? null
                : NormalizeNode(property.Value);
        }

        return normalizedObject;
    }

    if (node is JsonArray jsonArray)
    {
        var normalizedArray = new JsonArray();

        foreach (var item in jsonArray)
        {
            normalizedArray.Add(item is null ? null : NormalizeNode(item));
        }

        return normalizedArray;
    }

    return node.DeepClone();
}
