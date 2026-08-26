using MyFhirSdk.CodeGen;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Main_WithNoArguments_PrintsUsageAndReturnsNonZero()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();

        try
        {
            Console.SetError(error);

            var exitCode = Program.Main([]);

            Assert.NotEqual(0, exitCode);
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

            var exitCode = Program.Main(["--help"]);

            Assert.Equal(0, exitCode);
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task RunAsync_WithValidArguments_GeneratesRequestedTypes()
    {
        using var directory = new TestDirectory();
        var outputRoot = Path.Combine(directory.Path, "generated");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await RunCliAsync(
            CreateArguments(GetFixtureDirectory(), outputRoot, "HumanName", "Address"),
            directory.RepositoryRoot,
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Equal(
            ["Address.g.cs", "HumanName.g.cs"],
            Directory.EnumerateFiles(outputRoot)
                .Select(Path.GetFileName)
                .OrderBy(fileName => fileName, StringComparer.Ordinal));
        Assert.Contains("Generated ", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithSameDefinitionsInDifferentCreationOrder_IsDeterministic()
    {
        using var directory = new TestDirectory();
        var firstInput = Path.Combine(directory.Path, "input-a");
        var secondInput = Path.Combine(directory.Path, "input-b");
        CopyFixtures(firstInput, reverse: false);
        CopyFixtures(secondInput, reverse: true);
        var firstOutput = Path.Combine(directory.Path, "output-a");
        var secondOutput = Path.Combine(directory.Path, "output-b");
        using var firstStandardOutput = new StringWriter();
        using var firstError = new StringWriter();
        using var secondStandardOutput = new StringWriter();
        using var secondError = new StringWriter();

        var firstExitCode = await RunCliAsync(
            CreateArguments(firstInput, firstOutput, "Period", "HumanName", "Address"),
            directory.RepositoryRoot,
            firstStandardOutput,
            firstError);
        var secondExitCode = await RunCliAsync(
            CreateArguments(secondInput, secondOutput, "Address", "HumanName", "Period"),
            directory.RepositoryRoot,
            secondStandardOutput,
            secondError);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(firstExitCode, secondExitCode);
        Assert.Equal(firstError.ToString(), secondError.ToString());

        var firstFiles = GetOrderedFiles(firstOutput);
        var secondFiles = GetOrderedFiles(secondOutput);
        Assert.Equal(
            firstFiles.Select(Path.GetFileName),
            secondFiles.Select(Path.GetFileName));
        for (var index = 0; index < firstFiles.Length; index++)
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(firstFiles[index]),
                await File.ReadAllBytesAsync(secondFiles[index]));
        }
    }

    [Fact]
    public async Task RunAsync_WhenRequestedTypeIsMissing_LeavesOutputUntouched()
    {
        using var directory = new TestDirectory();
        var outputRoot = Path.Combine(directory.Path, "generated");
        Directory.CreateDirectory(outputRoot);
        var markerPath = Path.Combine(outputRoot, "keep.txt");
        await File.WriteAllTextAsync(markerPath, "keep");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await RunCliAsync(
            CreateArguments(GetFixtureDirectory(), outputRoot, "HumanName", "MissingType"),
            directory.RepositoryRoot,
            output,
            error);

        Assert.Equal(3, exitCode);
        Assert.Contains("[FSG0005]", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("MissingType", error.ToString(), StringComparison.Ordinal);
        Assert.Equal("keep", await File.ReadAllTextAsync(markerPath));
        Assert.Equal(["keep.txt"], Directory.EnumerateFiles(outputRoot).Select(Path.GetFileName));
    }

    [Fact]
    public async Task RunAsync_WithInvalidNamespace_ReturnsCliError()
    {
        using var directory = new TestDirectory();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var arguments = CreateArguments(
            GetFixtureDirectory(),
            Path.Combine(directory.Path, "generated"),
            "HumanName");
        arguments[5] = "Invalid-Namespace";

        var exitCode = await RunCliAsync(
            arguments,
            directory.RepositoryRoot,
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("not a valid C# namespace", error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "generated")));
    }

    [Fact]
    public async Task RunAsync_PrimitiveMode_GeneratesCompleteBatch()
    {
        using var directory = new TestDirectory();
        var outputRoot = Path.Combine(directory.Path, "primitives");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var generator = new FhirSdkGenerator(directory.RepositoryRoot);
        var cli = new GeneratorCli(
            generator,
            output,
            error,
            primitivePipeline: new PrimitiveGenerationPipeline(
                directory.RepositoryRoot));

        var exitCode = await cli.RunAsync(
        [
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
        Assert.Equal(19, Directory.EnumerateFiles(outputRoot).Count());
        Assert.Contains(
            "primitive-generation-manifest.json",
            output.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PrimitiveModeWithInvalidPolicy_ReturnsInputExitCode()
    {
        using var directory = new TestDirectory();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var generator = new FhirSdkGenerator(directory.RepositoryRoot);
        var cli = new GeneratorCli(
            generator,
            output,
            error,
            primitivePipeline: new PrimitiveGenerationPipeline(
                directory.RepositoryRoot));

        var exitCode = await cli.RunAsync(
        [
            "--mode", "primitive",
            "--input", GetPrimitiveFixtureDirectory(),
            "--policy", Path.Combine(directory.Path, "missing-policy.json"),
            "--output", Path.Combine(directory.Path, "primitives"),
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.Equal(2, exitCode);
        Assert.Contains("[FSG0013]", error.ToString(), StringComparison.Ordinal);
    }

    private static Task<int> RunCliAsync(
        string[] args,
        string repositoryRoot,
        TextWriter output,
        TextWriter error)
    {
        var generator = new FhirSdkGenerator(repositoryRoot);
        var cli = new GeneratorCli(generator, output, error);
        return cli.RunAsync(args);
    }

    private static string[] CreateArguments(
        string inputPath,
        string outputPath,
        params string[] typeNames)
    {
        var arguments = new List<string>
        {
            "--input", inputPath,
            "--output", outputPath,
            "--namespace", "MyFhirSdk.GeneratorFixtures.Types",
            "--fhir-version", "5.0.0"
        };
        foreach (var typeName in typeNames)
        {
            arguments.Add("--type");
            arguments.Add(typeName);
        }

        return arguments.ToArray();
    }

    private static string GetFixtureDirectory()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Valid");
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

    private static void CopyFixtures(string destination, bool reverse)
    {
        Directory.CreateDirectory(destination);
        var fixtures = Directory
            .EnumerateFiles(GetFixtureDirectory(), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (reverse)
        {
            Array.Reverse(fixtures);
        }

        foreach (var fixture in fixtures)
        {
            File.Copy(fixture, Path.Combine(destination, Path.GetFileName(fixture)));
        }
    }

    private static string[] GetOrderedFiles(string directory)
    {
        return Directory
            .EnumerateFiles(directory)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
    }

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
