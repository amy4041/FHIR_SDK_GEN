using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Inventory;

public sealed class PrimitiveInventoryPolicyJoinerTests
{
    private const string FhirVersion = "5.0.0";
    private const string PolicySourceFile = "primitive-generation-policy.json";

    private readonly PrimitiveInventoryPolicyJoiner _joiner = new();

    [Fact]
    public async Task Join_WithOfficialInventoryAndRepositoryPolicy_CoversEveryPrimitive()
    {
        var inventory = await LoadOfficialInventoryAsync();
        var policyDocument = await LoadRepositoryPolicyDocumentAsync();
        var policy = ValidatePolicy(policyDocument);

        var result = _joiner.Join(inventory, policy);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var coverage = Assert.IsType<PrimitiveInventoryPolicyCoverage>(result.Value);
        Assert.Same(inventory, coverage.Inventory);
        Assert.Same(policy, coverage.Policy);
        Assert.Equal(21, coverage.Matches.Count);
        Assert.Equal(
            17,
            coverage.Matches.Count(match => match.Policy.IsSupported));
        Assert.Equal(
            4,
            coverage.Matches.Count(match => !match.Policy.IsSupported));
        Assert.Equal(
            coverage.Matches
                .Select(match => match.Definition.FhirTypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            coverage.Matches.Select(match => match.Definition.FhirTypeName));
        Assert.All(
            coverage.Matches,
            match =>
            {
                Assert.Equal(
                    match.Definition.FhirTypeName,
                    match.Policy.FhirTypeName);
                Assert.Equal(match.Definition.Canonical, match.Policy.Canonical);
                Assert.Equal(match.Definition.FhirVersion, match.Policy.FhirVersion);
            });
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitiveInventoryPolicyMatch>)coverage.Matches).Clear());
    }

    [Fact]
    public async Task Join_WithInventoryOnlyEntry_ReturnsFsg0021()
    {
        var inventory = await LoadOfficialInventoryAsync();
        var document = await LoadRepositoryPolicyDocumentAsync();
        var policy = ValidatePolicy(CopyPolicy(
            document,
            document.Primitives!
                .Where(entry => entry?.FhirTypeName != "boolean")
                .ToArray()));

        var result = _joiner.Join(inventory, policy);

        AssertCoverageDiagnostic(
            result,
            GeneratorDiagnosticCodes.MissingPrimitivePolicyEntry,
            "'boolean'");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/boolean",
            diagnostic.DefinitionCanonical);
        Assert.Equal(
            "StructureDefinition-boolean.json",
            Path.GetFileName(diagnostic.SourceFile));
    }

    [Fact]
    public async Task Join_WithPolicyOnlyEntry_ReturnsFsg0022()
    {
        var inventory = await LoadOfficialInventoryAsync();
        var document = await LoadRepositoryPolicyDocumentAsync();
        var policy = ValidatePolicy(CopyPolicy(
            document,
            [.. document.Primitives!, CreateUnsupportedPolicyEntry("sample")]));

        var result = _joiner.Join(inventory, policy);

        AssertCoverageDiagnostic(
            result,
            GeneratorDiagnosticCodes.ExtraPrimitivePolicyEntry,
            "'sample'");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PolicySourceFile, diagnostic.SourceFile);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/sample",
            diagnostic.DefinitionCanonical);
    }

    [Fact]
    public void Join_WithCanonicalMismatch_ReturnsFsg0023()
    {
        var inventory = BuildInventory(
            CreateLoadedDefinition(
                "sample.json",
                "sample",
                canonical: "http://example.org/StructureDefinition/sample"),
            FhirVersion);
        var policy = ValidatePolicy(CreatePolicy(
            FhirVersion,
            CreateUnsupportedPolicyEntry("sample")));

        var result = _joiner.Join(inventory, policy);

        AssertCoverageDiagnostic(
            result,
            GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch,
            "inventory canonical");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains(
            "http://example.org/StructureDefinition/sample",
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "http://hl7.org/fhir/StructureDefinition/sample",
            diagnostic.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Join_WithFhirVersionMismatch_ReturnsSingleFsg0023()
    {
        var inventory = BuildInventory(
            CreateLoadedDefinition("sample.json", "sample"),
            FhirVersion);
        var policy = ValidatePolicy(CreatePolicy(
            "5.0.1",
            CreateUnsupportedPolicyEntry("sample", fhirVersion: "5.0.1")));

        var result = _joiner.Join(inventory, policy);

        AssertCoverageDiagnostic(
            result,
            GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch,
            "inventory FHIR version '5.0.0'");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PolicySourceFile, diagnostic.SourceFile);
        Assert.Equal("5.0.1", diagnostic.DefinitionVersion);
    }

    [Fact]
    public async Task Join_WithMultipleCoverageErrors_SortsDiagnosticsDeterministically()
    {
        var inventory = await LoadOfficialInventoryAsync();
        var document = await LoadRepositoryPolicyDocumentAsync();
        var policy = ValidatePolicy(CopyPolicy(
            document,
            [
                .. document.Primitives!
                    .Where(entry => entry?.FhirTypeName is not "boolean" and not "date"),
                CreateUnsupportedPolicyEntry("zeta"),
                CreateUnsupportedPolicyEntry("alpha")
            ]));

        var result = _joiner.Join(inventory, policy);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(4, result.Diagnostics.Count);
        Assert.Equal(
            result.Diagnostics
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal),
            result.Diagnostics);
    }

    private static async Task<PrimitiveDefinitionInventory>
        LoadOfficialInventoryAsync()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Primitives",
            "R5");
        var loadResult = await new StructureDefinitionLoader().LoadAsync(
            path,
            FhirVersion,
            StructureDefinitionLoadProfile.PrimitiveType);
        Assert.True(loadResult.IsSuccess);

        var inventoryResult = new PrimitiveDefinitionInventoryBuilder().Build(
            loadResult.Value,
            FhirVersion);
        Assert.True(inventoryResult.IsSuccess);
        return Assert.IsType<PrimitiveDefinitionInventory>(inventoryResult.Value);
    }

    private static PrimitiveDefinitionInventory BuildInventory(
        LoadedStructureDefinition definition,
        string expectedFhirVersion)
    {
        var result = new PrimitiveDefinitionInventoryBuilder().Build(
            [definition],
            expectedFhirVersion);

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveDefinitionInventory>(result.Value);
    }

    private static async Task<PrimitiveGenerationPolicyDocument>
        LoadRepositoryPolicyDocumentAsync()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json");
        var result = await new PrimitiveGenerationPolicyLoader().LoadAsync(path);

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveGenerationPolicyDocument>(result.Value);
    }

    private static ValidatedPrimitiveGenerationPolicy ValidatePolicy(
        PrimitiveGenerationPolicyDocument document)
    {
        var result = new PrimitiveGenerationPolicyValidator().Validate(
            document,
            PolicySourceFile);

        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value);
    }

    private static PrimitiveGenerationPolicyDocument CopyPolicy(
        PrimitiveGenerationPolicyDocument source,
        IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?> primitives)
    {
        return new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = source.SchemaVersion,
            PolicyVersion = source.PolicyVersion,
            FhirVersion = source.FhirVersion,
            RuntimeContractVersion = source.RuntimeContractVersion,
            PrimitiveNamespace = source.PrimitiveNamespace,
            Primitives = primitives
        };
    }

    private static PrimitiveGenerationPolicyDocument CreatePolicy(
        string fhirVersion,
        params PrimitiveGenerationPolicyEntryDocument[] primitives)
    {
        return new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = 1,
            PolicyVersion = "1.0.0",
            FhirVersion = fhirVersion,
            RuntimeContractVersion = "phase-a-v1",
            PrimitiveNamespace = "MyFhirSdk.Primitives",
            Primitives = primitives
        };
    }

    private static PrimitiveGenerationPolicyEntryDocument
        CreateUnsupportedPolicyEntry(
            string fhirTypeName,
            string fhirVersion = FhirVersion)
    {
        return new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = fhirTypeName,
            Canonical =
                $"http://hl7.org/fhir/StructureDefinition/{fhirTypeName}",
            FhirVersion = fhirVersion,
            SupportStatus = "unsupported",
            UnsupportedReason = "No approved Runtime contract."
        };
    }

    private static LoadedStructureDefinition CreateLoadedDefinition(
        string sourceFile,
        string type,
        string? canonical = null,
        string version = FhirVersion)
    {
        return new LoadedStructureDefinition(
            sourceFile,
            new StructureDefinitionDto
            {
                ResourceType = "StructureDefinition",
                Id = type,
                Url = canonical ??
                    $"http://hl7.org/fhir/StructureDefinition/{type}",
                Version = version,
                Name = type,
                Description = $"Documentation for {type}.",
                Type = type,
                Kind = "primitive-type",
                IsAbstract = false,
                BaseDefinition =
                    "http://hl7.org/fhir/StructureDefinition/PrimitiveType",
                Derivation = "specialization"
            });
    }

    private static void AssertCoverageDiagnostic(
        GenerationResult<PrimitiveInventoryPolicyCoverage?> result,
        string expectedCode,
        string messageFragment)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == expectedCode &&
                diagnostic.Message.Contains(
                    messageFragment,
                    StringComparison.Ordinal));
    }
}
