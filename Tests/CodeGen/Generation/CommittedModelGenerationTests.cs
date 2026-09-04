using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Policy;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class CommittedModelGenerationTests
{
    [Fact]
    public async Task OfficialFullBatch_MatchesCommittedGeneratedModelOutput()
    {
        var repositoryRoot = RepositoryRootLocator.Find(AppContext.BaseDirectory);
        string Policy(string name) => Path.Combine(repositoryRoot, "CodeGen", "Policy", name);
        var options = new ModelGenerationOptions(
            Path.Combine(repositoryRoot, "Tests", "CodeGen", "Fixtures", "FhirPackages", "R5",
                "hl7.fhir.r5.core-5.0.0.tgz"),
            Path.Combine(repositoryRoot, ".unused-c8-test-output"),
            "hl7.fhir.r5.core", "5.0.0", "5.0.0",
            Policy("primitive-generation-policy.json"),
            Policy("r5-model-ownership-policy.json"),
            new ModelIrPolicyPaths(
                Policy("r5-model-naming-policy.json"),
                Policy("r5-backbone-policy.json"),
                Policy("r5-choice-open-type-policy.json")),
            Policy("r5-validation-capability-policy.json"),
            [],
            ModelGenerationPipeline.DefaultCodeGenVersion);

        var result = await new ModelGenerationPipeline(repositoryRoot).BuildAsync(options);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        Assert.Equal(831, result.Value!.Manifest.Artifacts.Count);
        Assert.Equal(832, result.Value.Artifacts.Count);
        foreach (var artifact in result.Value!.Artifacts)
        {
            var committedPath = Path.Combine(
                repositoryRoot,
                artifact.FileName.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(committedPath), $"Missing committed artifact: {artifact.FileName}");
            Assert.Equal(
                Normalize(artifact.Content),
                Normalize(await File.ReadAllTextAsync(committedPath)));
        }

        var manifest = Assert.Single(
            result.Value.Artifacts,
            artifact => artifact.FileName == "Generated/R5/model-generation-manifest.json");
        Assert.Equal(
            "64a48eb35bd9378f4cef4c6d8677db7a87f8b0bc95315fad86c9ee91afb97f01",
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(Normalize(manifest.Content))))
                .ToLowerInvariant());
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
