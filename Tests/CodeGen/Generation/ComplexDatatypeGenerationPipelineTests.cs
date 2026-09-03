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
    public async Task OfficialFullDatatypeScope_IsBlockedByApprovedUnsupportedPrimitives()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var datatypeCanonicals = graph.Nodes
            .Where(node =>
                node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel &&
                string.Equals(node.Kind, "complex-type", StringComparison.Ordinal))
            .Select(node => node.Canonical)
            .ToArray();

        var result = new GenerationScopeSelector().Select(graph, datatypeCanonicals);

        Assert.Equal(39, datatypeCanonicals.Length);
        var blockedNames = graph.Nodes
            .Where(node => datatypeCanonicals.Contains(node.Canonical, StringComparer.Ordinal))
            .Where(node => !new GenerationScopeSelector().Select(graph, [node.Canonical]).IsSuccess)
            .Select(node => node.FhirTypeName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "Annotation",
                "Availability",
                "DataRequirement",
                "Dosage",
                "ElementDefinition",
                "ParameterDefinition",
                "SampledData",
                "Signature",
                "Timing",
                "TriggerDefinition",
                "UsageContext"
            },
            blockedNames);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedPrimitiveReference);
        Assert.Equal(
            new[] { "oid", "time", "uuid" },
            result.Diagnostics
                .Where(diagnostic => diagnostic.Code == GeneratorDiagnosticCodes.UnsupportedPrimitiveReference)
                .Select(diagnostic => diagnostic.Message.Split('\'')[3])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Generate_MaximalCurrentlySupportedOfficialDatatypeSet_CompilesTogether()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var supportedTypeNames = graph.Nodes
            .Where(node =>
                node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel &&
                string.Equals(node.Kind, "complex-type", StringComparison.Ordinal))
            .Where(node => new GenerationScopeSelector().Select(graph, [node.Canonical]).IsSuccess)
            .Select(node => node.FhirTypeName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(28, supportedTypeNames.Length);

        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(supportedTypeNames);
        var result = new ComplexDatatypeGenerationPipeline().Generate(ir);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        Assert.Equal(
            28,
            Assert.IsType<ComplexDatatypeGenerationBatch>(result.Value).Sources.Count);
    }
}
