using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.Serialization.Json;
using Xunit;

public sealed class FhirJsonSerializerFixtureTests
{
    public static IEnumerable<object[]> FixtureCases
    {
        get
        {
            return JsonTestCases.All.Select(testCase => new object[] { testCase });
        }
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void SerializeMatchesFixture(JsonFixtureTestCase testCase)
    {
        var serializer = new FhirJsonSerializer();

        var actualJson = serializer.Serialize(testCase.CreateResource());
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", testCase.FixtureFileName);
        var expectedJson = File.ReadAllText(fixturePath);

        Assert.Equal(NormalizeJson(expectedJson), NormalizeJson(actualJson));
    }

    private static string NormalizeJson(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON payload was empty.");
        var normalized = NormalizeNode(node);

        return normalized.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static JsonNode NormalizeNode(JsonNode node)
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
}
