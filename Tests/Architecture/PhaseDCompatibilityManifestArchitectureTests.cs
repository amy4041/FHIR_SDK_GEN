using MyFhirSdk.CodeGen.Compatibility;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PhaseDCompatibilityManifestArchitectureTests
{
    [Fact]
    public void CompatibilityMatrixAndManifestSchemasAreVersioned()
    {
        Assert.Equal(1, GenerationCompatibilityMatrix.SchemaVersion);
        Assert.Equal("exact", GenerationCompatibilityMatrix.VersionPolicy);
        Assert.Equal("MyFhirSdk.CodeGen.Tool",
            GenerationCompatibilityMatrix.ToolPackageId);
        Assert.Equal(2, PrimitiveGenerationManifestModel.CurrentSchemaVersion);
        Assert.Equal(2, ModelGenerationManifestModel.CurrentSchemaVersion);
    }

    [Fact]
    public void ManifestProvenanceCannotCarryPhysicalAssetPaths()
    {
        var propertyNames = typeof(GenerationManifestProvenance)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Path", StringComparison.Ordinal));
        Assert.Contains(nameof(GenerationManifestProvenance.RuntimeDescriptorSha256),
            propertyNames);
        Assert.Contains(nameof(GenerationManifestProvenance.CompilerReferenceLogicalIdentity),
            propertyNames);
    }

    [Fact]
    public void CliUsageRemainsACompileTimeConstant()
    {
        var field = typeof(GeneratorCommandLineParser).GetField(
            nameof(GeneratorCommandLineParser.Usage));

        Assert.NotNull(field);
        Assert.True(field.IsLiteral);
        Assert.False(field.IsInitOnly);
        Assert.Equal(GeneratorCommandLineParser.Usage, field.GetRawConstantValue());
    }
}
