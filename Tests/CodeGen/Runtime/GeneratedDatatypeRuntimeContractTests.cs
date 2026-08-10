using System.Text.Json.Nodes;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;
using GeneratedHumanName = MyFhirSdk.GeneratorFixtures.Types.HumanName;
using GeneratedPeriod = MyFhirSdk.GeneratorFixtures.Types.Period;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class GeneratedDatatypeRuntimeContractTests
{
    private const string ContractJson =
        """
        {
          "resourceType": "GeneratedDatatypeContainer",
          "name": {
            "use": "official",
            "text": "Alice Example",
            "_text": {
              "id": "text-metadata"
            },
            "given": [
              "Alice",
              null
            ],
            "_given": [
              null,
              {
                "id": "given-metadata"
              }
            ],
            "period": {
              "start": "2020-01-01T00:00:00Z"
            }
          }
        }
        """;

    [Fact]
    public void Serialize_GeneratedDatatype_UsesExistingRuntimeContract()
    {
        var resource = CreateContractResource();

        var json = new FhirJsonSerializer().Serialize(resource);

        AssertJsonEqual(ContractJson, json);
    }

    [Fact]
    public void Parse_ConcreteGeneratedDatatype_RestoresCompleteObjectGraph()
    {
        var resource = new FhirJsonParser()
            .Parse<GeneratedDatatypeContainer>(ContractJson);

        var name = Assert.IsType<GeneratedHumanName>(resource.Name);
        Assert.Equal("official", name.Use?.Value);
        Assert.Equal("Alice Example", name.Text?.Value);
        Assert.Equal("text-metadata", name.Text?.Id);
        Assert.Null(name.Family);

        Assert.Collection(
            name.Given,
            given =>
            {
                Assert.Equal("Alice", given.Value);
                Assert.Null(given.Id);
            },
            given =>
            {
                Assert.Null(given.Value);
                Assert.Equal("given-metadata", given.Id);
            });
        Assert.Empty(name.Prefix);
        Assert.Empty(name.Suffix);

        var period = Assert.IsType<GeneratedPeriod>(name.Period);
        Assert.Equal("2020-01-01T00:00:00Z", period.Start?.Value);
        Assert.Null(period.End);
    }

    [Fact]
    public void SerializeParseSerialize_GeneratedDatatype_IsStable()
    {
        var serializer = new FhirJsonSerializer();
        var parser = new FhirJsonParser();
        var firstJson = serializer.Serialize(CreateContractResource());

        var parsed = parser.Parse<GeneratedDatatypeContainer>(firstJson);
        var secondJson = serializer.Serialize(parsed);

        AssertJsonEqual(firstJson, secondJson);
    }

    [Fact]
    public void Validate_GeneratedDatatype_ReportsNestedPrimitivePaths()
    {
        var resource = new GeneratedDatatypeContainer
        {
            Name = new GeneratedHumanName
            {
                Given = [new FhirString("")],
                Period = new GeneratedPeriod
                {
                    Start = new FhirDateTime("2020-99-99")
                }
            }
        };

        var result = new FhirValidator().Validate(resource);

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Issues.OrderBy(issue => issue.Path, StringComparer.Ordinal),
            issue => AssertPrimitiveFormatIssue(
                issue,
                "GeneratedDatatypeContainer.name.given[0]"),
            issue => AssertPrimitiveFormatIssue(
                issue,
                "GeneratedDatatypeContainer.name.period.start"));
    }

    private static GeneratedDatatypeContainer CreateContractResource()
    {
        return new GeneratedDatatypeContainer
        {
            Name = new GeneratedHumanName
            {
                Use = new FhirCode("official"),
                Text = new FhirString("Alice Example")
                {
                    Id = "text-metadata"
                },
                Given =
                [
                    new FhirString("Alice"),
                    new FhirString { Id = "given-metadata" }
                ],
                Period = new GeneratedPeriod
                {
                    Start = new FhirDateTime("2020-01-01T00:00:00Z")
                }
            }
        };
    }

    private static void AssertPrimitiveFormatIssue(
        ValidationIssue issue,
        string expectedPath)
    {
        Assert.Equal(expectedPath, issue.Path);
        Assert.Equal(ValidationIssueCode.PrimitiveFormat, issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        var actualNode = JsonNode.Parse(actual);

        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"JSON differs.{Environment.NewLine}" +
            $"Expected: {expected}{Environment.NewLine}" +
            $"Actual:   {actual}");
    }
}
