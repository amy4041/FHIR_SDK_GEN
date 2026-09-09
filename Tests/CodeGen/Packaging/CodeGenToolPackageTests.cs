using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MyFhirSdk.CodeGen.Compatibility;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Packaging;

public sealed class CodeGenToolPackageTests
{
    private const string PackageId = GenerationCompatibilityMatrix.ToolPackageId;
    private const string PackageVersion = GenerationCompatibilityMatrix.ToolVersion;
    private const string ToolCommand = "myfhir-codegen";
    private static readonly string ToolRoot =
        $"tools/{GenerationCompatibilityMatrix.TargetFramework}/any/";

    [Fact]
    public void PackageInventoryAndMetadataMatchAcceptedContract()
    {
        using var package = OpenPackage("first");
        var actualInventory = package.Entries
            .Select(entry => NormalizeContainerEntry(entry.FullName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var expectedInventory = File.ReadAllLines(Path.Combine(
            AppContext.BaseDirectory,
            "Packaging",
            "codegen-tool-package-layout.txt"));

        Assert.Equal(expectedInventory, actualInventory);
        Assert.DoesNotContain(package.Entries, entry =>
            IsForbiddenEntry(entry.FullName));
        Assert.Single(package.Entries, entry => string.Equals(
            entry.FullName,
            ToolRoot + "Assets/RuntimeReferences/" +
            GenerationCompatibilityMatrix.TargetFramework +
            "/MyFhirSdk.dll",
            StringComparison.Ordinal));
        AssertPackageDoesNotContainRepositoryPath(package);

        var nuspec = ReadXml(
            package,
            PackageId + ".nuspec");
        XNamespace ns = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd";
        var metadata = Assert.IsType<XElement>(nuspec.Root?.Element(ns + "metadata"));
        Assert.Equal(PackageId, metadata.Element(ns + "id")?.Value);
        Assert.Equal(PackageVersion, metadata.Element(ns + "version")?.Value);
        Assert.Equal("MyFhirSdk contributors", metadata.Element(ns + "authors")?.Value);
        Assert.Null(metadata.Element(ns + "license"));
        Assert.Equal("README.md", metadata.Element(ns + "readme")?.Value);
        Assert.Equal("DotnetTool", metadata
            .Element(ns + "packageTypes")?
            .Element(ns + "packageType")?
            .Attribute("name")?
            .Value);
        Assert.Equal(
            "https://github.com/amy4041/FHIR_SDK_GEN",
            metadata.Element(ns + "repository")?.Attribute("url")?.Value);

        var settings = ReadXml(
            package,
            ToolRoot + "DotnetToolSettings.xml");
        var command = Assert.IsType<XElement>(settings
            .Root?
            .Element("Commands")?
            .Element("Command"));
        Assert.Equal(ToolCommand, command.Attribute("Name")?.Value);
        Assert.Equal("MyFhirSdk.CodeGen.dll", command.Attribute("EntryPoint")?.Value);
        Assert.Equal("dotnet", command.Attribute("Runner")?.Value);
    }

    [Fact]
    public void PackageAssetsMatchManifestProvenanceAndDescriptorHashes()
    {
        using var package = OpenPackage("first");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "CommittedGenerated",
            "R5",
            "model-generation-manifest.json")));
        var compatibility = manifest.RootElement.GetProperty("compatibility");

        Assert.Equal(PackageId, compatibility
            .GetProperty("tool")
            .GetProperty("packageId")
            .GetString());
        Assert.Equal(PackageVersion, compatibility
            .GetProperty("tool")
            .GetProperty("version")
            .GetString());
        Assert.Equal(
            GenerationCompatibilityMatrix.TargetFramework,
            compatibility.GetProperty("targetFramework").GetString());

        var descriptorBytes = ReadBytes(
            package,
            ToolRoot + "Contracts/runtime-contract.json");
        Assert.Equal(
            compatibility
                .GetProperty("runtimeDescriptor")
                .GetProperty("sha256")
                .GetString(),
            Sha256(descriptorBytes));

        var referenceBytes = ReadBytes(
            package,
            ToolRoot + "Assets/RuntimeReferences/" +
            GenerationCompatibilityMatrix.TargetFramework +
            "/MyFhirSdk.dll");
        Assert.Equal(
            compatibility
                .GetProperty("compilerReference")
                .GetProperty("sha256")
                .GetString(),
            Sha256(referenceBytes));

        using var descriptor = JsonDocument.Parse(descriptorBytes);
        var descriptorCompatibility = descriptor.RootElement.GetProperty("compatibility");
        Assert.Equal(PackageVersion,
            descriptorCompatibility.GetProperty("toolVersion").GetString());
        Assert.Equal(
            descriptorCompatibility.GetProperty("primitivePolicy").GetProperty("sha256").GetString(),
            NormalizedTextSha256(ReadBytes(
                package,
                ToolRoot + "Policy/primitive-generation-policy.json")));

        var policyFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["backbone"] = "r5-backbone-policy.json",
            ["choice-open-type"] = "r5-choice-open-type-policy.json",
            ["model-naming"] = "r5-model-naming-policy.json",
            ["model-ownership"] = "r5-model-ownership-policy.json",
            ["validation-capability"] = "r5-validation-capability-policy.json"
        };
        foreach (var policy in descriptorCompatibility.GetProperty("modelPolicies")
                     .EnumerateArray())
        {
            var name = Assert.IsType<string>(policy.GetProperty("name").GetString());
            Assert.True(policyFiles.TryGetValue(name, out var fileName));
            Assert.Equal(
                policy.GetProperty("sha256").GetString(),
                NormalizedTextSha256(ReadBytes(
                    package,
                    ToolRoot + "Policy/" + fileName)));
        }
    }

    [Fact]
    public void TwoPacksHaveIdenticalNormalizedPayloads()
    {
        using var first = OpenPackage("first");
        using var second = OpenPackage("second");

        Assert.Equal(PayloadHashes(first), PayloadHashes(second));
    }

    [Theory]
    [InlineData("PackageId", "Different.Tool")]
    [InlineData("PackageVersion", "9.9.9")]
    [InlineData("ToolCommandName", "different-command")]
    [InlineData("PackAsTool", "false")]
    public async Task PackRejectsToolIdentityOverrides(
        string propertyName,
        string propertyValue)
    {
        using var directory = new TestDirectory();
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(directory.Path, "dotnet-home"),
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["NUGET_PACKAGES"] = Path.Combine(directory.Path, "nuget-packages")
        };
        var runtimeReference = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "RuntimeReferences",
            GenerationCompatibilityMatrix.TargetFramework,
            "MyFhirSdk.dll");

        var result = await RunDotNetAsync(
            GetRepositoryRoot(),
            environment,
            "pack",
            Path.Combine(GetRepositoryRoot(), "CodeGen", "MyFhirSdk.CodeGen.csproj"),
            "--configuration", "Release",
            "--no-build",
            "--no-restore",
            "--output", directory.Path,
            "-p:RuntimeReferenceAssetPath=" + runtimeReference,
            "-p:" + propertyName + "=" + propertyValue);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "Tool package contract violation:",
            result.StandardOutput + result.StandardError,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalManifestRestoreRunsHelpPrimitiveAndModelOutsideRepository()
    {
        using var directory = new TestDirectory();
        var manifestDirectory = Path.Combine(directory.Path, ".config");
        Directory.CreateDirectory(manifestDirectory);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "RepositoryToolManifest", "dotnet-tools.json"),
            Path.Combine(manifestDirectory, "dotnet-tools.json"));

        var packageSource = Path.GetDirectoryName(GetPackagePath("first"))!;
        var nugetConfig = Path.Combine(directory.Path, "NuGet.Config");
        await File.WriteAllTextAsync(
            nugetConfig,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="d6-local" value="PACKAGE_SOURCE" />
              </packageSources>
            </configuration>
            """.Replace("PACKAGE_SOURCE", packageSource, StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(directory.Path, "dotnet-home"),
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["NUGET_PACKAGES"] = Path.Combine(directory.Path, "nuget-packages")
        };

        AssertSuccess(await RunDotNetAsync(
            directory.Path,
            environment,
            "tool", "restore", "--configfile", nugetConfig));
        var help = await RunDotNetAsync(
            directory.Path,
            environment,
            ToolCommand, "--help");
        AssertSuccess(help);
        Assert.Contains(
            PackageId + " " + PackageVersion,
            help.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            "Command: " + ToolCommand,
            help.StandardOutput,
            StringComparison.Ordinal);
        var invalid = await RunDotNetAsync(
            directory.Path,
            environment,
            ToolCommand);
        Assert.NotEqual(0, invalid.ExitCode);
        Assert.Contains("Usage:", invalid.StandardError, StringComparison.Ordinal);

        var primitiveOutput = Path.Combine(directory.Path, "primitive-output");
        AssertSuccess(await RunDotNetAsync(
            directory.Path,
            environment,
            ToolCommand,
            "--mode", "primitive",
            "--input", Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "StructureDefinitions",
                "Primitives",
                "R5"),
            "--policy", Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "primitive-generation-policy.json"),
            "--output", primitiveOutput,
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"));
        Assert.True(File.Exists(Path.Combine(
            primitiveOutput,
            "primitive-generation-manifest.json")));

        var modelOutput = Path.Combine(directory.Path, "model-output");
        AssertSuccess(await RunDotNetAsync(
            directory.Path,
            environment,
            ToolCommand,
            "--mode", "model",
            "--input", Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "FhirPackages",
                "R5",
                "hl7.fhir.r5.core-5.0.0.tgz"),
            "--output", modelOutput,
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0",
            "--canonical", "http://hl7.org/fhir/StructureDefinition/Patient"));
        Assert.True(File.Exists(Path.Combine(
            modelOutput,
            "Generated",
            "R5",
            "model-generation-manifest.json")));
    }

    private static bool IsForbiddenEntry(string path) =>
        path.Contains("Fixtures/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Tests/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static void AssertPackageDoesNotContainRepositoryPath(ZipArchive package)
    {
        var repositoryRoot = GetRepositoryRoot();
        var candidates = new[]
        {
            repositoryRoot,
            repositoryRoot.Replace('\\', '/'),
            repositoryRoot.Replace('/', '\\')
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in package.Entries)
        {
            var bytes = ReadBytes(entry);
            var singleByteText = Encoding.Latin1.GetString(bytes);
            var utf16Text = Encoding.Unicode.GetString(bytes);
            foreach (var candidate in candidates)
            {
                Assert.DoesNotContain(
                    candidate,
                    singleByteText,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    candidate,
                    utf16Text,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string NormalizeContainerEntry(string path) =>
        path.StartsWith(
            "package/services/metadata/core-properties/",
            StringComparison.Ordinal)
            ? "package/services/metadata/core-properties/<generated>.psmdcp"
            : path;

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    private static SortedDictionary<string, string> PayloadHashes(ZipArchive package) =>
        new(package.Entries
            .Where(entry => !IsNuGetContainerMetadata(entry.FullName))
            .ToDictionary(
                entry => entry.FullName,
                entry => Sha256(ReadBytes(entry)),
                StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static bool IsNuGetContainerMetadata(string path) =>
        string.Equals(path, "[Content_Types].xml", StringComparison.Ordinal) ||
        string.Equals(path, "_rels/.rels", StringComparison.Ordinal) ||
        path.StartsWith(
            "package/services/metadata/core-properties/",
            StringComparison.Ordinal);

    private static ZipArchive OpenPackage(string packName) =>
        ZipFile.OpenRead(GetPackagePath(packName));

    private static string GetPackagePath(string packName) => Path.Combine(
        AppContext.BaseDirectory,
        "ToolPackages",
        packName,
        PackageId + "." + PackageVersion + ".nupkg");

    private static XDocument ReadXml(ZipArchive package, string path)
    {
        var entry = package.GetEntry(path);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static byte[] ReadBytes(ZipArchive package, string path)
    {
        var entry = package.GetEntry(path);
        Assert.NotNull(entry);
        return ReadBytes(entry);
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string NormalizedTextSha256(byte[] content)
    {
        var text = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(content);
        return Sha256(Encoding.UTF8.GetBytes(
            text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')));
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static async Task<ProcessResult> RunDotNetAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (var variable in environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static void AssertSuccess(ProcessResult result) =>
        Assert.True(
            result.ExitCode == 0,
            $"dotnet exited with {result.ExitCode}.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{result.StandardError}");

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MyFhirSdk-D6-ToolTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
