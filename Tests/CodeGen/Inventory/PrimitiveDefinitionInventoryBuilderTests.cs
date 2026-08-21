using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Inventory;

public sealed class PrimitiveDefinitionInventoryBuilderTests
{
    private const string FhirVersion = "5.0.0";

    private readonly PrimitiveDefinitionInventoryBuilder _builder = new();

    [Fact]
    public async Task Build_WithOfficialR5Fixtures_ProducesOrderedImmutableInventory()
    {
        var loadedDefinitions = await LoadOfficialPrimitiveDefinitionsAsync();

        var result = _builder.Build(loadedDefinitions, FhirVersion);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var inventory = Assert.IsType<PrimitiveDefinitionInventory>(result.Value);
        Assert.Equal(FhirVersion, inventory.FhirVersion);
        Assert.Equal(21, inventory.Items.Count);
        Assert.Equal(
            inventory.Items
                .Select(item => item.FhirTypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            inventory.Items.Select(item => item.FhirTypeName));

        var boolean = Assert.Single(
            inventory.Items,
            item => item.FhirTypeName == "boolean");
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/boolean",
            boolean.Canonical);
        Assert.Equal(FhirVersion, boolean.FhirVersion);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/PrimitiveType",
            boolean.BaseDefinition);
        Assert.Equal("boolean", boolean.DefinitionName);
        Assert.Contains("true", boolean.Description, StringComparison.Ordinal);
        Assert.Equal(
            "StructureDefinition-boolean.json",
            Path.GetFileName(boolean.SourceFile));
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitiveDefinitionInventoryItem>)inventory.Items).Clear());
    }

    [Fact]
    public async Task Build_WithShuffledInput_ProducesIdenticalOrdinalInventory()
    {
        var loadedDefinitions = await LoadOfficialPrimitiveDefinitionsAsync();

        var original = _builder.Build(loadedDefinitions, FhirVersion);
        var reversed = _builder.Build(
            loadedDefinitions.Reverse().ToArray(),
            FhirVersion);

        Assert.Equal(
            Describe(Assert.IsType<PrimitiveDefinitionInventory>(original.Value)),
            Describe(Assert.IsType<PrimitiveDefinitionInventory>(reversed.Value)));
    }

    [Fact]
    public void Build_WithDuplicateTypeAndCanonical_ReturnsFsg0020ForEachKey()
    {
        var definitions = new[]
        {
            CreateLoadedDefinition("z-source.json", "sample"),
            CreateLoadedDefinition("a-source.json", "sample")
        };

        var result = _builder.Build(definitions, FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(
            2,
            result.Diagnostics.Count(diagnostic =>
                diagnostic.Code ==
                GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains(
                "FHIR type name 'sample'",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains(
                "canonical 'http://hl7.org/fhir/StructureDefinition/sample'",
                StringComparison.Ordinal));
        Assert.All(
            result.Diagnostics,
            diagnostic => Assert.Equal("z-source.json", diagnostic.SourceFile));
    }

    [Fact]
    public void Build_WithDuplicateCanonicalAcrossTypes_ReturnsFsg0020()
    {
        const string canonical =
            "http://hl7.org/fhir/StructureDefinition/alpha";
        var definitions = new[]
        {
            CreateLoadedDefinition("alpha.json", "alpha", canonical: canonical),
            CreateLoadedDefinition("beta.json", "beta", canonical: canonical)
        };

        var result = _builder.Build(definitions, FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code ==
                    GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry &&
                diagnostic.Message.Contains(
                    $"canonical '{canonical}'",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Build_PreservesCanonicalForPolicyCoverageValidation()
    {
        var definition = CreateLoadedDefinition(
            "sample.json",
            "sample",
            canonical: "http://example.org/StructureDefinition/sample");

        var result = _builder.Build([definition], FhirVersion);

        Assert.True(result.IsSuccess);
        var inventory = Assert.IsType<PrimitiveDefinitionInventory>(result.Value);
        var item = Assert.Single(inventory.Items);
        Assert.Equal(
            "http://example.org/StructureDefinition/sample",
            item.Canonical);
    }

    [Fact]
    public void Build_UniquenessAndOrderingAreOrdinalAndCaseSensitive()
    {
        var definitions = new[]
        {
            CreateLoadedDefinition("lower.json", "alpha"),
            CreateLoadedDefinition("upper.json", "Alpha")
        };

        var result = _builder.Build(definitions, FhirVersion);

        Assert.True(result.IsSuccess);
        var inventory = Assert.IsType<PrimitiveDefinitionInventory>(result.Value);
        Assert.Equal(
            ["Alpha", "alpha"],
            inventory.Items.Select(item => item.FhirTypeName));
    }

    [Theory]
    [InlineData("Patient", "primitive-type", "5.0.0", GeneratorDiagnosticCodes.InvalidPrimitiveInventory)]
    [InlineData("StructureDefinition", "complex-type", "5.0.0", GeneratorDiagnosticCodes.UnsupportedDefinition)]
    [InlineData("StructureDefinition", "primitive-type", "4.0.1", GeneratorDiagnosticCodes.FhirVersionMismatch)]
    public void Build_WithDefinitionOutsideSelectionContract_ReturnsDiagnostic(
        string resourceType,
        string kind,
        string version,
        string expectedCode)
    {
        var definition = CreateLoadedDefinition(
            "sample.json",
            "sample",
            resourceType: resourceType,
            kind: kind,
            version: version);

        var result = _builder.Build([definition], FhirVersion);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public void Build_DiagnosticsAreDeterministicForShuffledInput()
    {
        var definitions = new[]
        {
            CreateLoadedDefinition("z-source.json", "sample"),
            CreateLoadedDefinition("a-source.json", "sample")
        };

        var original = _builder.Build(definitions, FhirVersion);
        var reversed = _builder.Build(definitions.Reverse().ToArray(), FhirVersion);

        Assert.Equal(original.Diagnostics, reversed.Diagnostics);
        Assert.Equal(
            original.Diagnostics
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal),
            original.Diagnostics);
    }

    private static async Task<IReadOnlyList<LoadedStructureDefinition>>
        LoadOfficialPrimitiveDefinitionsAsync()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Primitives",
            "R5");
        var result = await new StructureDefinitionLoader().LoadAsync(
            path,
            FhirVersion,
            StructureDefinitionLoadProfile.PrimitiveType);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static LoadedStructureDefinition CreateLoadedDefinition(
        string sourceFile,
        string type,
        string? canonical = null,
        string resourceType = "StructureDefinition",
        string kind = "primitive-type",
        string version = FhirVersion)
    {
        return new LoadedStructureDefinition(
            sourceFile,
            new StructureDefinitionDto
            {
                ResourceType = resourceType,
                Id = type,
                Url = canonical ??
                    $"http://hl7.org/fhir/StructureDefinition/{type}",
                Version = version,
                Name = type,
                Description = $"Documentation for {type}.",
                Type = type,
                Kind = kind,
                IsAbstract = false,
                BaseDefinition =
                    "http://hl7.org/fhir/StructureDefinition/PrimitiveType",
                Derivation = "specialization"
            });
    }

    private static string[] Describe(PrimitiveDefinitionInventory inventory)
    {
        return inventory.Items
            .Select(item => string.Join(
                '|',
                item.FhirTypeName,
                item.Canonical,
                item.FhirVersion,
                item.BaseDefinition,
                item.DefinitionName,
                item.Description,
                Path.GetFileName(item.SourceFile)))
            .ToArray();
    }
}
