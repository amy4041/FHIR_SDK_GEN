using MyFhirSdk.CodeGen.Definitions;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Inventory;

public sealed class DefinitionInventoryBuilderTests
{
    private static readonly DefinitionPackageIdentity R5Identity = new(
        "hl7.fhir.r5.core",
        "5.0.0",
        "Core",
        "5.0.0");

    private readonly DefinitionInventoryBuilder _builder = new();

    [Fact]
    public async Task Pipeline_WithApprovedOfficialPackage_ProducesCompleteInventory()
    {
        var pipeline = new DefinitionInventoryPipeline();
        var result = await pipeline.BuildAsync(
            new FileDefinitionPackageInput(GetOfficialPackagePath()),
            new DefinitionPackageLoadOptions(
                "hl7.fhir.r5.core",
                "5.0.0",
                "5.0.0"));

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        var inventory = Assert.IsType<DefinitionInventory>(result.Value);
        Assert.Equal(R5Identity, inventory.PackageIdentity);
        Assert.Equal(307, inventory.Items.Count);
        AssertCategoryCount(inventory, DefinitionInventoryCategory.ModelRoot, 1);
        AssertCategoryCount(inventory, DefinitionInventoryCategory.ModelSpecialization, 209);
        AssertCategoryCount(inventory, DefinitionInventoryCategory.PrimitiveSpecialization, 21);
        AssertCategoryCount(inventory, DefinitionInventoryCategory.ConstraintProfile, 66);
        AssertCategoryCount(inventory, DefinitionInventoryCategory.LogicalModel, 10);
        Assert.Equal(
            inventory.Items
                .OrderBy(item => item.Canonical, StringComparer.Ordinal)
                .ThenBy(item => item.SourceIdentity, StringComparer.Ordinal)
                .Select(item => item.SourceIdentity),
            inventory.Items.Select(item => item.SourceIdentity));

        var patient = Assert.Single(
            inventory.Items,
            item =>
                item.Category == DefinitionInventoryCategory.ModelSpecialization &&
                item.FhirTypeName == "Patient");
        Assert.Equal("Patient", patient.Id);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/Patient",
            patient.Canonical);
        Assert.Equal("5.0.0", patient.DefinitionVersion);
        Assert.Equal("5.0.0", patient.FhirVersion);
        Assert.Equal("resource", patient.Kind);
        Assert.False(patient.IsAbstract);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/DomainResource",
            patient.BaseDefinition);
        Assert.Equal("specialization", patient.Derivation);
        Assert.Equal(
            "package/StructureDefinition-Patient.json",
            patient.SourceIdentity);
        Assert.NotNull(patient.Definition.Snapshot?.Elements);

        var root = Assert.Single(
            inventory.Items,
            item => item.Category == DefinitionInventoryCategory.ModelRoot);
        Assert.Equal("Base", root.FhirTypeName);
        Assert.Null(root.BaseDefinition);
        Assert.Null(root.Derivation);
        Assert.Equal(
            2,
            inventory.Items.Count(item =>
                item.Category == DefinitionInventoryCategory.ConstraintProfile &&
                item.Definition.Snapshot?.Elements is null));
        Assert.Throws<NotSupportedException>(
            () => ((IList<DefinitionInventoryItem>)inventory.Items).Clear());
    }

    [Fact]
    public void Build_WithMixedApprovedCategories_ClassifiesEveryDefinition()
    {
        var package = CreatePackage(
            CreateDefinition("Base", "complex-type", derivation: null, baseDefinition: null),
            CreateDefinition("Patient", "resource"),
            CreateDefinition("string", "primitive-type", baseDefinition: PrimitiveBase),
            CreateDefinition(
                "PatientProfile",
                "resource",
                derivation: "constraint",
                canonical: "http://example.org/PatientProfile",
                includeSnapshot: false),
            CreateDefinition("Event", "logical", derivation: null));

        var result = _builder.Build(package);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var inventory = Assert.IsType<DefinitionInventory>(result.Value);
        Assert.Equal(
            [
                DefinitionInventoryCategory.ModelRoot,
                DefinitionInventoryCategory.ModelSpecialization,
                DefinitionInventoryCategory.PrimitiveSpecialization,
                DefinitionInventoryCategory.ConstraintProfile,
                DefinitionInventoryCategory.LogicalModel
            ],
            inventory.Items
                .OrderBy(item => item.Category)
                .Select(item => item.Category));
    }

    [Fact]
    public void Build_AllowsConstraintProfileToReuseBaseFhirType()
    {
        var package = CreatePackage(
            CreateDefinition("Patient", "resource"),
            CreateDefinition(
                "Patient",
                "resource",
                derivation: "constraint",
                canonical: "http://example.org/PatientProfile",
                sourceIdentity: "package/StructureDefinition-PatientProfile.json"));

        var result = _builder.Build(package);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        Assert.Equal(2, Assert.IsType<DefinitionInventory>(result.Value).Items.Count);
    }

    [Fact]
    public void Build_WithReorderedDefinitions_ProducesIdenticalInventory()
    {
        var definitions = new[]
        {
            CreateDefinition("Patient", "resource"),
            CreateDefinition("Address", "complex-type"),
            CreateDefinition("string", "primitive-type", baseDefinition: PrimitiveBase)
        };

        var original = _builder.Build(CreatePackage(definitions));
        var reversed = _builder.Build(CreatePackage(definitions.Reverse().ToArray()));

        Assert.True(original.IsSuccess, Describe(original.Diagnostics));
        Assert.True(reversed.IsSuccess, Describe(reversed.Diagnostics));
        Assert.Equal(
            Snapshot(Assert.IsType<DefinitionInventory>(original.Value)),
            Snapshot(Assert.IsType<DefinitionInventory>(reversed.Value)));
    }

    [Theory]
    [InlineData("type")]
    [InlineData("canonical")]
    [InlineData("source")]
    public void Build_WithDuplicateIdentity_ReturnsDeterministicFsg0029(
        string duplicateField)
    {
        var first = CreateDefinition(
            "Alpha",
            "complex-type",
            canonical: "http://example.org/Alpha",
            sourceIdentity: "package/StructureDefinition-Alpha.json");
        var second = duplicateField switch
        {
            "type" => CreateDefinition(
                "Alpha",
                "resource",
                canonical: "http://example.org/Other",
                sourceIdentity: "package/StructureDefinition-Other.json"),
            "canonical" => CreateDefinition(
                "Other",
                "resource",
                canonical: "http://example.org/Alpha",
                sourceIdentity: "package/StructureDefinition-Other.json"),
            "source" => CreateDefinition(
                "Other",
                "resource",
                canonical: "http://example.org/Other",
                sourceIdentity: "package/StructureDefinition-Alpha.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(duplicateField))
        };

        var original = _builder.Build(CreatePackage(first, second));
        var reversed = _builder.Build(CreatePackage(second, first));

        Assert.False(original.IsSuccess);
        Assert.Null(original.Value);
        Assert.Contains(
            original.Diagnostics,
            diagnostic =>
                diagnostic.Code ==
                GeneratorDiagnosticCodes.DuplicateDefinitionInventoryEntry);
        Assert.Equal(original.Diagnostics, reversed.Diagnostics);
    }

    [Fact]
    public void Build_WithWrongSelectedVersionAndMissingSnapshot_FailsBeforeGraph()
    {
        var definition = CreateDefinition(
            "Patient",
            "resource",
            version: "4.0.1",
            fhirVersion: "4.0.1",
            includeSnapshot: false);

        var result = _builder.Build(CreatePackage(definition));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == GeneratorDiagnosticCodes.FhirVersionMismatch);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == GeneratorDiagnosticCodes.MissingSnapshot);
    }

    [Fact]
    public void Build_WithUnapprovedKindAndDerivation_ReturnsUnsupportedDefinition()
    {
        var definition = CreateDefinition(
            "Unknown",
            "logical",
            derivation: "specialization");

        var result = _builder.Build(CreatePackage(definition));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedDefinition);
    }

    [Fact]
    public void Build_WithEmptyPackage_ReturnsInvalidInventory()
    {
        var result = _builder.Build(CreatePackage());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == GeneratorDiagnosticCodes.InvalidDefinitionInventory &&
                diagnostic.Message.Contains("empty", StringComparison.Ordinal));
    }

    private static LoadedDefinitionPackage CreatePackage(
        params LoadedStructureDefinition[] definitions) =>
        new(R5Identity, definitions);

    private static LoadedStructureDefinition CreateDefinition(
        string type,
        string kind,
        string? derivation = "specialization",
        string? baseDefinition = ModelBase,
        string? canonical = null,
        string? sourceIdentity = null,
        string? version = "5.0.0",
        string? fhirVersion = "5.0.0",
        bool includeSnapshot = true)
    {
        var root = new ElementDefinitionDto
        {
            Id = type,
            Path = type,
            Min = 0,
            Max = "*"
        };
        return new LoadedStructureDefinition(
            sourceIdentity ?? $"package/StructureDefinition-{type}.json",
            new StructureDefinitionDto
            {
                ResourceType = "StructureDefinition",
                Id = type,
                Url = canonical ?? $"http://hl7.org/fhir/StructureDefinition/{type}",
                Version = version,
                FhirVersion = fhirVersion,
                Name = type,
                Type = type,
                Kind = kind,
                IsAbstract = type == "Base",
                BaseDefinition = baseDefinition,
                Derivation = derivation,
                Snapshot = includeSnapshot
                    ? new StructureDefinitionSnapshotDto { Elements = [root] }
                    : null,
                Differential = includeSnapshot
                    ? new StructureDefinitionDifferentialDto { Elements = [root] }
                    : null
            });
    }

    private static void AssertCategoryCount(
        DefinitionInventory inventory,
        DefinitionInventoryCategory category,
        int expected) =>
        Assert.Equal(expected, inventory.Items.Count(item => item.Category == category));

    private static string[] Snapshot(DefinitionInventory inventory) =>
        inventory.Items.Select(item => string.Join(
            '|',
            item.SourceIdentity,
            item.Id,
            item.FhirTypeName,
            item.Canonical,
            item.DefinitionVersion,
            item.FhirVersion,
            item.Kind,
            item.IsAbstract,
            item.BaseDefinition,
            item.Derivation,
            item.Category)).ToArray();

    private static string GetOfficialPackagePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message));

    private const string ModelBase =
        "http://hl7.org/fhir/StructureDefinition/DataType";

    private const string PrimitiveBase =
        "http://hl7.org/fhir/StructureDefinition/PrimitiveType";
}
