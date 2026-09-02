using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Ir;

public sealed class R5ModelIrTests
{
    [Fact]
    public async Task Build_WithOfficialPeriodScope_ProducesRendererReadyIr()
    {
        var (graph, policy) = await BuildOfficialContext();
        var scopeResult = new GenerationScopeSelector().Select(
            graph,
            ["http://hl7.org/fhir/StructureDefinition/Period"]);
        Assert.True(scopeResult.IsSuccess, Describe(scopeResult.Diagnostics));

        var result = new ModelIrBuilder().Build(
            graph,
            Assert.IsType<GenerationScope>(scopeResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var batch = Assert.IsType<ModelIrBatch>(result.Value);
        var period = Assert.Single(batch.Declarations);
        Assert.Equal(ModelIrCategory.ComplexDatatype, period.Category);
        Assert.Equal("Period", period.FhirName);
        Assert.Equal("MyFhirSdk.Types.Period", period.FullyQualifiedName);
        Assert.Equal("Generated/R5/Types/Period.g.cs", period.ArtifactPath);
        Assert.False(period.IsAbstract);
        Assert.False(period.IsSealed);
        Assert.Equal("MyFhirSdk.Core.DataType", period.BaseType.ClrType);
        Assert.True(period.BaseType.IsAbstractTarget);
        Assert.True(period.BaseType.IsExternal);
        Assert.Equal(new[] { "end", "start" }, period.Members.Select(member => member.FhirName).Order());
        Assert.All(period.Members, member =>
        {
            Assert.Equal(ModelMemberRepresentation.Standard, member.Representation);
            Assert.Equal("MyFhirSdk.Primitives.FhirDateTime", Assert.Single(member.Properties).CSharpType);
            Assert.Equal(member.Source.DefinitionCanonical, period.Source.DefinitionCanonical);
            Assert.NotNull(member.Source.ElementId);
            Assert.NotNull(member.Source.ElementPath);
        });
    }

    [Fact]
    public async Task LoadPolicy_WithC0Decisions_ProducesCompositeIrPolicy()
    {
        var result = await LoadPolicy();

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var policy = Assert.IsType<ModelIrGenerationPolicy>(result.Value);
        Assert.Equal("MyFhirSdk.Types", policy.DatatypeNamespace);
        Assert.Equal("MyFhirSdk.Resources", policy.ResourceNamespace);
        Assert.Equal("MyFhirSdk.Core.DataType", policy.OpenTypeClrType);
        Assert.Contains("Parameters.parameter.value[x]", policy.OpenTypeElementIds);
        Assert.Equal("ReferenceValue", policy.MemberRenames["Reference.reference"].ClrName);
        Assert.Equal("ClaimDetail", policy.BackboneRenames["Claim.item.detail"].ClrName);
    }

    [Fact]
    public async Task LoadPolicy_WithMissingInput_ReturnsDiagnostic()
    {
        var result = await new ModelIrGenerationPolicyLoader().LoadAsync(
            new ModelIrPolicyPaths(
                Path.Combine(Path.GetTempPath(), "missing-model-naming-policy.json"),
                PolicyPath("r5-backbone-policy.json"),
                PolicyPath("r5-choice-open-type-policy.json")));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(
            GeneratorDiagnosticCodes.ModelIrPolicyReadFailure,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Build_WithOfficialReferenceScope_AppliesApprovedMemberRename()
    {
        var (graph, policy) = await BuildOfficialContext();
        var scopeResult = new GenerationScopeSelector().Select(
            graph,
            ["http://hl7.org/fhir/StructureDefinition/Reference"]);
        Assert.True(scopeResult.IsSuccess, Describe(scopeResult.Diagnostics));
        var result = new ModelIrBuilder().Build(
            graph,
            Assert.IsType<GenerationScope>(scopeResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            policy);

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var reference = Assert.Single(
            Assert.IsType<ModelIrBatch>(result.Value).Declarations,
            declaration => declaration.FhirName == "Reference");
        var property = Assert.Single(
            reference.Members,
            member => member.Source.ElementId == "Reference.reference").Properties.Single();
        Assert.Equal("ReferenceValue", property.CSharpName);
        Assert.Equal("reference", property.JsonName);
    }

    private static async Task<(DefinitionDependencyGraph Graph, ModelIrGenerationPolicy Policy)>
        BuildOfficialContext()
    {
        var inventoryResult = await new DefinitionInventoryPipeline().BuildAsync(
            new FileDefinitionPackageInput(GetOfficialPackagePath()),
            new DefinitionPackageLoadOptions("hl7.fhir.r5.core", "5.0.0", "5.0.0"));
        Assert.True(inventoryResult.IsSuccess, Describe(inventoryResult.Diagnostics));
        var ownershipPath = PolicyPath("r5-model-ownership-policy.json");
        var ownershipResult = await new ModelOwnershipPolicyLoader().LoadAsync(ownershipPath);
        Assert.True(ownershipResult.IsSuccess, Describe(ownershipResult.Diagnostics));
        var graphResult = new DefinitionDependencyGraphBuilder().Build(
            Assert.IsType<DefinitionInventory>(inventoryResult.Value),
            PrimitivePolicyTestContext.GetMappingView(),
            Assert.IsType<ModelOwnershipPolicyDocument>(ownershipResult.Value),
            ownershipPath);
        Assert.True(graphResult.IsSuccess, Describe(graphResult.Diagnostics));
        var policyResult = await LoadPolicy();
        Assert.True(policyResult.IsSuccess, Describe(policyResult.Diagnostics));
        return (
            Assert.IsType<DefinitionDependencyGraph>(graphResult.Value),
            Assert.IsType<ModelIrGenerationPolicy>(policyResult.Value));
    }

    internal static Task<GenerationResult<ModelIrGenerationPolicy?>> LoadPolicy() =>
        new ModelIrGenerationPolicyLoader().LoadAsync(new ModelIrPolicyPaths(
            PolicyPath("r5-model-naming-policy.json"),
            PolicyPath("r5-backbone-policy.json"),
            PolicyPath("r5-choice-open-type-policy.json")));

    private static string PolicyPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Policy", fileName);

    private static string GetOfficialPackagePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

    internal static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.Code} {diagnostic.DefinitionCanonical} {diagnostic.ElementId}: {diagnostic.Message}"));
}
