using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Parsing;

public sealed class MvpDatatypeParserTests
{
    private const string FhirVersion = "5.0.0";
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";

    private static readonly string[] MvpTypeNames =
        ["Period", "Coding", "HumanName", "Address", "Identifier"];

    private static readonly IReadOnlySet<string> MvpPreviewTypes =
        new HashSet<string>(MvpTypeNames, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, ExpectedProperty[]>
        ExpectedProperties =
            new Dictionary<string, ExpectedProperty[]>(StringComparer.Ordinal)
            {
                ["Period"] =
                [
                    Primitive("Start", "FhirDateTime"),
                    Primitive("End", "FhirDateTime")
                ],
                ["Coding"] =
                [
                    Primitive("System", "FhirUri"),
                    Primitive("Version", "FhirString"),
                    Primitive("Code", "FhirCode"),
                    Primitive("Display", "FhirString"),
                    Primitive("UserSelected", "FhirBoolean")
                ],
                ["HumanName"] =
                [
                    Primitive("Use", "FhirCode"),
                    Primitive("Text", "FhirString"),
                    Primitive("Family", "FhirString"),
                    Primitive("Given", "FhirString", isCollection: true),
                    Primitive("Prefix", "FhirString", isCollection: true),
                    Primitive("Suffix", "FhirString", isCollection: true),
                    Preview("Period", "Period")
                ],
                ["Address"] =
                [
                    Primitive("Use", "FhirCode"),
                    Primitive("Type", "FhirCode"),
                    Primitive("Text", "FhirString"),
                    Primitive("Line", "FhirString", isCollection: true),
                    Primitive("City", "FhirString"),
                    Primitive("District", "FhirString"),
                    Primitive("State", "FhirString"),
                    Primitive("PostalCode", "FhirString"),
                    Primitive("Country", "FhirString"),
                    Preview("Period", "Period")
                ],
                ["Identifier"] =
                [
                    Primitive("Use", "FhirCode"),
                    SdkType("Type", "CodeableConcept"),
                    Primitive("System", "FhirUri"),
                    Primitive("Value", "FhirString"),
                    Preview("Period", "Period"),
                    SdkType("Assigner", "Reference")
                ]
            };

    [Fact]
    public async Task Parse_OfficialMvpFixtures_ReturnsCompleteInternalModels()
    {
        foreach (var typeName in MvpTypeNames)
        {
            var model = await ParseFixtureAsync(typeName);
            var expectedProperties = ExpectedProperties[typeName];

            Assert.Equal(typeName, model.FhirName);
            Assert.Equal(typeName, model.CSharpName);
            Assert.Equal(TargetNamespace, model.Namespace);
            Assert.Equal("MyFhirSdk.Core.DataType", model.CSharpBaseType);
            Assert.False(model.IsAbstract);
            Assert.Equal(
                $"http://hl7.org/fhir/StructureDefinition/{typeName}",
                model.SourceCanonical);
            Assert.Equal(FhirVersion, model.SourceVersion);
            Assert.Equal(expectedProperties.Length, model.Properties.Count);
            Assert.Equal(
                Enumerable.Range(0, expectedProperties.Length),
                model.Properties.Select(property => property.Order));

            for (var index = 0; index < expectedProperties.Length; index++)
            {
                var expected = expectedProperties[index];
                var actual = model.Properties[index];

                Assert.Equal(expected.CSharpName, actual.CSharpName);
                Assert.Equal(ToLowerCamelCase(expected.CSharpName), actual.FhirName);
                Assert.Equal(expected.CSharpType, actual.CSharpType);
                Assert.Equal(expected.IsCollection, actual.IsCollection);
                Assert.False(actual.IsRequired);
                Assert.Equal(0, actual.Min);
                Assert.Equal(expected.IsCollection ? "*" : "1", actual.Max);
                Assert.False(string.IsNullOrWhiteSpace(actual.Documentation));
            }
        }
    }

    [Fact]
    public async Task Generate_MvpBatchInDifferentInputOrders_ReturnsStableSources()
    {
        var forwardBatch = await GenerateBatchAsync(MvpTypeNames);
        var reverseBatch = await GenerateBatchAsync(MvpTypeNames.Reverse());

        Assert.Equal(5, forwardBatch.Count);
        Assert.Equal(
            forwardBatch.Keys.OrderBy(name => name, StringComparer.Ordinal),
            reverseBatch.Keys.OrderBy(name => name, StringComparer.Ordinal));

        foreach (var typeName in MvpTypeNames)
        {
            Assert.Equal(forwardBatch[typeName].Source, reverseBatch[typeName].Source);
            Assert.Equal(
                forwardBatch[typeName].PropertyNames,
                reverseBatch[typeName].PropertyNames);
        }

        AssertPreviewReference(forwardBatch["HumanName"].Model, "Period");
        AssertPreviewReference(forwardBatch["Address"].Model, "Period");
        AssertPreviewReference(forwardBatch["Identifier"].Model, "Period");
    }

    private static async Task<IReadOnlyDictionary<string, GeneratedType>>
        GenerateBatchAsync(IEnumerable<string> inputTypeNames)
    {
        var generatedTypes = new Dictionary<string, GeneratedType>(
            StringComparer.Ordinal);
        var renderer = new CSharpClassRenderer();

        foreach (var typeName in inputTypeNames)
        {
            var model = await ParseFixtureAsync(typeName);
            generatedTypes.Add(
                typeName,
                new GeneratedType(
                    model,
                    renderer.Render(model),
                    model.Properties
                        .Select(property => property.CSharpName)
                        .ToArray()));
        }

        return generatedTypes;
    }

    private static async Task<FhirTypeModel> ParseFixtureAsync(string typeName)
    {
        var fixturePath = GetFixturePath(typeName);
        var loadResult = await new StructureDefinitionLoader().LoadAsync(
            fixturePath,
            FhirVersion);

        Assert.True(loadResult.IsSuccess);
        Assert.Empty(loadResult.Diagnostics);
        var loadedDefinition = Assert.Single(loadResult.Value);

        var parseResult = new StructureDefinitionParser().Parse(
            loadedDefinition,
            TargetNamespace,
            MvpPreviewTypes);

        Assert.True(parseResult.IsSuccess);
        Assert.Empty(parseResult.Diagnostics);
        return Assert.IsType<FhirTypeModel>(parseResult.Value);
    }

    private static string GetFixturePath(string typeName)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Valid",
            $"StructureDefinition-{typeName}.json");
    }

    private static void AssertPreviewReference(
        FhirTypeModel model,
        string propertyName)
    {
        var property = Assert.Single(
            model.Properties,
            item => item.CSharpName == propertyName);
        Assert.Equal($"{TargetNamespace}.{propertyName}", property.CSharpType);
    }

    private static string ToLowerCamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static ExpectedProperty Primitive(
        string propertyName,
        string typeName,
        bool isCollection = false)
    {
        return new ExpectedProperty(
            propertyName,
            $"MyFhirSdk.Primitives.{typeName}",
            isCollection);
    }

    private static ExpectedProperty Preview(
        string propertyName,
        string typeName)
    {
        return new ExpectedProperty(
            propertyName,
            $"{TargetNamespace}.{typeName}",
            IsCollection: false);
    }

    private static ExpectedProperty SdkType(
        string propertyName,
        string typeName)
    {
        return new ExpectedProperty(
            propertyName,
            $"MyFhirSdk.Types.{typeName}",
            IsCollection: false);
    }

    private sealed record ExpectedProperty(
        string CSharpName,
        string CSharpType,
        bool IsCollection);

    private sealed record GeneratedType(
        FhirTypeModel Model,
        string Source,
        IReadOnlyList<string> PropertyNames);
}
