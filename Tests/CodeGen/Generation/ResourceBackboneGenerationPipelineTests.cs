using System.Reflection;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class ResourceBackboneGenerationPipelineTests
{
    [Fact]
    public async Task Generate_PatientClosure_RendersResourcesBackbonesAndDatatypesAsOneBatch()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");

        var result = new ResourceBackboneGenerationPipeline().Generate(ir);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var batch = Assert.IsType<ResourceBackboneGenerationBatch>(result.Value);
        Assert.Contains(batch.Sources, source =>
            source.FileName == "Generated/R5/Resources/Patient/Patient.g.cs");
        Assert.Contains(batch.Sources, source =>
            source.FileName == "Generated/R5/Resources/Patient/PatientContact.g.cs");
        Assert.Contains(batch.Sources, source =>
            source.FileName.StartsWith("Generated/R5/Types/", StringComparison.Ordinal));
        Assert.Equal(
            batch.Sources.OrderBy(source => source.FileName, StringComparer.Ordinal),
            batch.Sources);
    }

    [Fact]
    public async Task Generate_WithoutBackboneOwner_FailsBeforeCompilation()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");
        var constructor = Assert.Single(typeof(ModelIrBatch).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        var withoutPatient = Assert.IsType<ModelIrBatch>(constructor.Invoke(
            [
                ir.Declarations.Where(declaration => declaration.FhirName != "Patient"),
                ir.ExternalMetadata
            ]));

        var result = new ResourceBackboneGenerationPipeline().Generate(withoutPatient);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.MissingDependency &&
            diagnostic.Message.Contains("resource owner", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generate_OfficialFullScope_CompilesAllResourcesBackbonesAndDatatypesTogether()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var allGeneratedNames = graph.Nodes
            .Where(node => node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel)
            .Select(node => node.FhirTypeName)
            .ToArray();
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(allGeneratedNames);
        var pipeline = new ResourceBackboneGenerationPipeline();

        var result = pipeline.Generate(ir);
        var constructor = Assert.Single(typeof(ModelIrBatch).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        var reversedIr = Assert.IsType<ModelIrBatch>(constructor.Invoke(
            [ir.Declarations.Reverse(), ir.ExternalMetadata.Reverse()]));
        var repeated = pipeline.Generate(reversedIr);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        Assert.True(repeated.IsSuccess, ComplexDatatypeTestContext.Describe(repeated.Diagnostics));
        Assert.Equal(160, ir.Declarations.Count(declaration => declaration.Category == ModelIrCategory.Resource));
        Assert.Equal(613, ir.Declarations.Count(declaration => declaration.Category == ModelIrCategory.Backbone));
        Assert.Equal(39, ir.Declarations.Count(declaration => declaration.Category == ModelIrCategory.ComplexDatatype));
        Assert.Equal(17, ir.Declarations.Count(declaration => declaration.Category == ModelIrCategory.ComplexDatatypeComponent));
        var batch = Assert.IsType<ResourceBackboneGenerationBatch>(result.Value);
        Assert.Equal(829, batch.Sources.Count);
        Assert.Equal(
            batch.Artifacts,
            Assert.IsType<ResourceBackboneGenerationBatch>(repeated.Value).Artifacts);
        Assert.All(
            ir.Declarations.Where(declaration => declaration.Category == ModelIrCategory.Backbone),
            declaration => Assert.StartsWith(
                $"Generated/R5/Resources/{GetOwnerName(declaration.ResourceOwnerCanonical!)}/",
                declaration.ArtifactPath,
                StringComparison.Ordinal));
        var concreteResources = ir.Declarations.Where(declaration =>
                declaration.Category == ModelIrCategory.Resource &&
                !declaration.IsAbstract)
            .ToArray();
        Assert.Equal(
            concreteResources.Length,
            concreteResources.Select(resource => resource.FhirName).Distinct(StringComparer.Ordinal).Count());
        foreach (var resource in concreteResources)
        {
            var source = Assert.Single(batch.Sources, candidate =>
                candidate.FileName == resource.ArtifactPath).Source;
            Assert.Contains(
                $"public override string ResourceType => \"{resource.FhirName}\";",
                source,
                StringComparison.Ordinal);
        }
    }

    private static string GetOwnerName(string canonical) =>
        canonical[(canonical.LastIndexOf('/') + 1)..];
}
