using MyFhirSdk.CodeGen;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task RunAsync_ModelModeInvalidPackage_PrintsDiagnosticAndReturnsInputExitCode()
    {
        using var directory = new TestDirectory();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new GeneratorCli(
            output,
            error,
            modelPipeline: new ModelGenerationPipeline(directory.RepositoryRoot));

        var exitCode = await cli.RunAsync([
            "--mode", "model", "--input", Path.Combine(directory.Path, "missing.tgz"),
            "--output", Path.Combine(directory.Path, "output"), "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core", "--package-version", "5.0.0"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("[FSG0026]", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void Main_WithNoArguments_PrintsUsageAndReturnsNonZero()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Console.SetError(error);
            Assert.NotEqual(0, Program.Main([]));
            Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Main_WithHelp_PrintsUsageAndReturnsZero()
    {
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, Program.Main(["--help"]));
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task RunAsync_PrimitiveMode_GeneratesCompleteBatch()
    {
        using var directory = new TestDirectory();
        var outputRoot = Path.Combine(directory.Path, "primitives");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new GeneratorCli(
            output,
            error,
            primitivePipeline: new PrimitiveGenerationPipeline(directory.RepositoryRoot));

        var exitCode = await cli.RunAsync([
            "--mode", "primitive",
            "--input", GetPrimitiveFixtureDirectory(),
            "--policy", GetPrimitivePolicyPath(),
            "--output", outputRoot,
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Equal(22, Directory.EnumerateFiles(outputRoot).Count());
        Assert.Contains("primitive-generation-manifest.json", output.ToString(), StringComparison.Ordinal);
    }

    private static string GetPrimitiveFixtureDirectory() => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "StructureDefinitions",
        "Primitives",
        "R5");

    private static string GetPrimitivePolicyPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        "primitive-generation-policy.json");

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MyFhirSdk-CodeGen-CliTests",
                Guid.NewGuid().ToString("N"));
            RepositoryRoot = System.IO.Path.Combine(Path, "repository");
            Directory.CreateDirectory(RepositoryRoot);
        }

        public string Path { get; }

        public string RepositoryRoot { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
