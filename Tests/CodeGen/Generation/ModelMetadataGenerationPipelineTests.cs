using System.Reflection;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Tests.Metadata;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class ModelMetadataGenerationPipelineTests
{
    [Fact]
    public async Task Generate_OfficialSelectedPatientScope_CompilesScopedMetadata()
    {
        var (_, modelIr) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");
        Assert.DoesNotContain(modelIr.Declarations, declaration => declaration.FhirName == "Age");

        var result = new ModelMetadataGenerationPipeline().Generate(modelIr);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var batch = Assert.IsType<ModelMetadataGenerationBatch>(result.Value);
        Assert.Equal(modelIr.Declarations.Count + 2, batch.CompilationSources.Count);
        Assert.InRange(batch.Metadata.ExtensionValues.Count, 1, 53);
        var metadataSource = Assert.Single(batch.Sources, source =>
            source.FileName == ModelMetadataRenderer.ArtifactPath).Source;
        Assert.Contains(
            "static () => new global::MyFhirSdk.Resources.Patient()",
            metadataSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "typeof(global::MyFhirSdk.Types.Age)",
            metadataSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_OfficialFullScope_CompilesModelsMetadataFactoriesAndRulesTogether()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();

        var result = new ModelMetadataGenerationPipeline().Generate(modelIr);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var batch = Assert.IsType<ModelMetadataGenerationBatch>(result.Value);
        Assert.Equal(2, batch.Sources.Count);
        Assert.Equal(831, batch.CompilationSources.Count);
        Assert.Contains(batch.Sources, source => source.FileName == ModelMetadataRenderer.ArtifactPath);
        Assert.Contains(batch.Sources, source =>
            source.FileName == ValidationCompositionRenderer.ArtifactPath);
        var metadataSource = Assert.Single(batch.Sources, source =>
            source.FileName == ModelMetadataRenderer.ArtifactPath).Source;
        var validationSource = Assert.Single(batch.Sources, source =>
            source.FileName == ValidationCompositionRenderer.ArtifactPath).Source;
        Assert.Contains("\"Patient\"", metadataSource, StringComparison.Ordinal);
        Assert.Contains(
            "static () => new global::MyFhirSdk.Resources.Patient()",
            metadataSource,
            StringComparison.Ordinal);
        Assert.Contains("\"valueString\"", metadataSource, StringComparison.Ordinal);
        Assert.Contains(
            "ChoiceElementRule<global::MyFhirSdk.Resources.Patient>.AtMostOne(\"deceased[x]\"",
            validationSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_ReversedIr_ProducesByteIdenticalMetadataArtifacts()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();
        var constructor = Assert.Single(typeof(ModelIrBatch).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        var reversed = Assert.IsType<ModelIrBatch>(constructor.Invoke(
            [modelIr.Declarations.Reverse(), modelIr.ExternalMetadata.Reverse()]));
        var pipeline = new ModelMetadataGenerationPipeline();

        var first = pipeline.Generate(modelIr);
        var second = pipeline.Generate(reversed);

        Assert.True(first.IsSuccess, ComplexDatatypeTestContext.Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, ComplexDatatypeTestContext.Describe(second.Diagnostics));
        Assert.Equal(
            Assert.IsType<ModelMetadataGenerationBatch>(first.Value).Artifacts,
            Assert.IsType<ModelMetadataGenerationBatch>(second.Value).Artifacts);
    }
}
