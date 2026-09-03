using System.Reflection;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class ComplexDatatypeGenerationPipelineTests
{
    [Fact]
    public async Task Generate_OfficialReferenceClosure_RendersAndCompilesAsOneBatch()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Reference");

        var result = new ComplexDatatypeGenerationPipeline().Generate(ir);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var batch = Assert.IsType<ComplexDatatypeGenerationBatch>(result.Value);
        Assert.NotEmpty(batch.Sources);
        Assert.Equal(batch.Sources.Count, batch.Artifacts.Count);
        Assert.Contains(batch.Sources, source =>
            source.FileName == "Generated/R5/Types/Reference.g.cs");
        Assert.Equal(
            batch.Sources.OrderBy(source => source.FileName, StringComparer.Ordinal),
            batch.Sources);
    }

    [Fact]
    public async Task Generate_Twice_ProducesByteIdenticalArtifacts()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Reference");
        var pipeline = new ComplexDatatypeGenerationPipeline();

        var first = pipeline.Generate(ir);
        var second = pipeline.Generate(ir);

        Assert.True(first.IsSuccess, ComplexDatatypeTestContext.Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, ComplexDatatypeTestContext.Describe(second.Diagnostics));
        Assert.Equal(
            Assert.IsType<ComplexDatatypeGenerationBatch>(first.Value).Artifacts,
            Assert.IsType<ComplexDatatypeGenerationBatch>(second.Value).Artifacts);
    }

    [Fact]
    public async Task Generate_ExistingFiveMvpTypes_CompilesWithGraphDerivedDependencies()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(
            "Address",
            "Coding",
            "HumanName",
            "Identifier",
            "Period");

        var result = new ComplexDatatypeGenerationPipeline().Generate(ir);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var names = Assert.IsType<ComplexDatatypeGenerationBatch>(result.Value).Sources
            .Select(source => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(source.FileName)))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Address", names);
        Assert.Contains("Coding", names);
        Assert.Contains("HumanName", names);
        Assert.Contains("Identifier", names);
        Assert.Contains("Period", names);
    }

    [Fact]
    public async Task Generate_WithMissingGeneratedDependency_FailsBeforeCompilation()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Duration");
        var constructor = Assert.Single(typeof(ModelIrBatch).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        var durationOnly = Assert.IsType<ModelIrBatch>(constructor.Invoke(
            [ir.Declarations.Where(declaration => declaration.FhirName == "Duration")]));

        var result = new ComplexDatatypeGenerationPipeline().Generate(durationOnly);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.MissingDependency &&
            diagnostic.Message.Contains("Quantity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_OfficialFullDatatypeScope_CompilesAllThirtyNineTogether()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var datatypeCanonicals = graph.Nodes
            .Where(node =>
                node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel &&
                string.Equals(node.Kind, "complex-type", StringComparison.Ordinal))
            .Select(node => node.Canonical)
            .ToArray();

        Assert.Equal(39, datatypeCanonicals.Length);
        var typeNames = graph.Nodes
            .Where(node => datatypeCanonicals.Contains(node.Canonical, StringComparer.Ordinal))
            .Select(node => node.FhirTypeName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(typeNames);
        var pipeline = new ComplexDatatypeGenerationPipeline();
        var result = pipeline.Generate(ir);
        var repeated = pipeline.Generate(ir);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        Assert.True(repeated.IsSuccess, ComplexDatatypeTestContext.Describe(repeated.Diagnostics));
        var batch = Assert.IsType<ComplexDatatypeGenerationBatch>(result.Value);
        Assert.Equal(
            batch.Artifacts,
            Assert.IsType<ComplexDatatypeGenerationBatch>(repeated.Value).Artifacts);
        Assert.Equal(
            39,
            ir.Declarations.Count(declaration =>
                declaration.Category == ModelIrCategory.ComplexDatatype));
        Assert.Equal(
            17,
            ir.Declarations.Count(declaration =>
                declaration.Category == ModelIrCategory.ComplexDatatypeComponent));
        Assert.Equal(56, batch.Sources.Count);
        Assert.Contains(batch.Sources, source =>
            source.FileName ==
                "Generated/R5/Types/DataRequirement/DataRequirementDateFilter.g.cs");
        Assert.Contains(batch.Sources, source =>
            source.FileName ==
                "Generated/R5/Types/ElementDefinition/ElementDefinitionBase.g.cs");
        Assert.Contains(
            "public sealed class DataRequirementDateFilter : Element",
            Assert.Single(batch.Sources, source =>
                source.FileName.EndsWith(
                    "DataRequirementDateFilter.g.cs",
                    StringComparison.Ordinal)).Source);
    }
}
