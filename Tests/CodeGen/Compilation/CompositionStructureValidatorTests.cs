using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Rendering;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Compilation;

public sealed class CompositionStructureValidatorTests
{
    private static GeneratedSource[] Sources =>
    [
        new(ModelMetadataRenderer.ArtifactPath, "namespace MyFhirSdk.ModelMetadata.R5; internal static class GeneratedR5ModelMetadata { internal static object Create() => new(); }"),
        new(ValidationCompositionRenderer.ArtifactPath, "namespace MyFhirSdk.ModelMetadata.R5; internal static class GeneratedR5ValidationRules { internal static object Create() => new(); }")
    ];

    [Fact]
    public void ValidStructureDoesNotClaimSdkSemanticCompatibility()
    {
        var sources = Sources;
        sources[0] = sources[0] with { Source = sources[0].Source.Replace("object Create() => new()", "MissingSdkType Create() => MissingFactory.Create()") };
        Assert.True(new CompositionStructureValidator().Validate(sources).IsSuccess);
    }

    [Theory]
    [InlineData("Create()", "Build()")]
    [InlineData("internal static class", "public class")]
    [InlineData("ModelMetadata.R5", "WrongNamespace")]
    [InlineData("=> new();", "=> ;")]
    public void MalformedCompositionFailsBeforeWriting(string original, string replacement)
    {
        var sources = Sources;
        sources[0] = sources[0] with { Source = sources[0].Source.Replace(original, replacement, StringComparison.Ordinal) };
        Assert.False(new CompositionStructureValidator().Validate(sources).IsSuccess);
    }

    [Fact]
    public void MissingOrDuplicateArtifactIsRejected()
    {
        var validator = new CompositionStructureValidator();
        Assert.False(validator.Validate(Sources.Take(1).ToArray()).IsSuccess);
        Assert.False(validator.Validate([Sources[0], Sources[0]]).IsSuccess);
    }
}
