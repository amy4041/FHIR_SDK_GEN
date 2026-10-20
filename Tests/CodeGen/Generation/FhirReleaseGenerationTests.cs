using System.Reflection;
using System.Runtime.Loader;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Tests.Runtime;
using MyFhirSdk.CodeGen.Writing;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class FhirReleaseGenerationTests : IDisposable
{
    private readonly string _output = Path.Combine(Path.GetTempPath(), "MyFhirSdk-V4-" + Guid.NewGuid().ToString("N"));
    private static string Root => RealSdkSourceCompiler.RepositoryRoot();
    private static readonly Lazy<Task<(ModelGenerationBatch Models, PrimitiveGenerationBatch Primitives)>> R4 = new(async () =>
    {
        var (pipeline, primitive, options) = await Create();
        var first = await pipeline.BuildAsync(options);
        var second = await pipeline.BuildAsync(options);
        Assert.True(first.IsSuccess, Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, Describe(second.Diagnostics));
        Assert.Equal(first.Value!.Artifacts.Select(a => (a.FileName, a.Content)), second.Value!.Artifacts.Select(a => (a.FileName, a.Content)));
        var p = await primitive.BuildAsync(new(options.PackagePath, options.PrimitivePolicyPath, options.OutputPath,
            "4.0.1", "hl7.fhir.r4.core", "4.0.1", PrimitiveGenerationPipeline.DefaultCodeGenVersion));
        Assert.True(p.IsSuccess, Describe(p.Diagnostics));
        return (first.Value, p.Value!);
    });

    [Fact]
    public async Task FullR4BatchIsDeterministicAndUsesR4NamespacesPathsAndPrimitiveRules()
    {
        var (models, primitives) = await R4.Value;
        Assert.All(models.Artifacts, a => Assert.StartsWith("Generated/R4/", a.FileName));
        Assert.Contains(models.Sources, a => a.FileName == "Generated/R4/Resources/Patient/Patient.g.cs");
        Assert.Contains(models.Sources, a => a.Source.Contains("GeneratedR4ModelMetadata"));
        Assert.All(models.Sources, a => Assert.DoesNotContain("MyFhirSdk.Resources.", a.Source));
        var attachment = Assert.Single(models.Sources, a => a.FileName.EndsWith("/Attachment.g.cs"));
        Assert.Contains("using MyFhirSdk.R4.Primitives;", attachment.Source);
        Assert.Contains("FhirUnsignedInt? Size", attachment.Source);
        Assert.DoesNotContain(primitives.Sources, a => a.FileName == "FhirInteger64.g.cs");
        Assert.Equal("FHIR R4", primitives.Manifest.FhirSpecification);
        Assert.Equal(19, primitives.Manifest.Primitives.Count(p => p.SupportStatus == "supported"));
    }

    [Fact]
    public async Task FreshR4CompositionCompilesAndRunsWithProductionContextAndEngines()
    {
        var (models, primitives) = await R4.Value;
        var sources = models.Sources.Concat(primitives.Sources.Select(s => s with { FileName = "Generated/R4/Primitives/" + s.FileName })).ToArray();
        var compilation = RealSdkSourceCompiler.CreateR4Models(sources);
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)));
        image.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(image);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name == "MyFhirSdk");
        var provider = assembly.GetType("V4FixtureProvider", true)!;
        T Get<T>(string name) => (T)provider.GetProperty(name)!.GetValue(null)!;
        var context = new FhirModelContext(Get<ModelMetadataDefinition>("Metadata"), Get<PrimitiveProviderDefinition>("Primitives"), Get<ValidationProviderDefinition>("Validation"));
        const string json = "{\"resourceType\":\"Patient\",\"active\":true,\"photo\":[{\"size\":42}]}";
        var resource = new FhirJsonParser(context).Parse<Resource>(json);
        Assert.Equal("MyFhirSdk.R4.Resources.Patient", resource.GetType().FullName);
        Assert.True(new FhirValidator(context).Validate(resource).IsValid);
        Assert.Contains("\"size\":42", new FhirJsonSerializer(context).Serialize(resource));
        Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer().Serialize(resource));
        var observation = context.CreateResource("Observation");
        Assert.Contains(new FhirValidator(context).Validate(observation).Issues, i => i.Path == "Observation.status");
    }

    [Theory]
    [InlineData("r5-policy", GeneratorDiagnosticCodes.IncompatiblePrimitivePolicy)]
    [InlineData("r5-contract", GeneratorDiagnosticCodes.IncompatibleFhirPackage)]
    [InlineData("r5-auxiliary", GeneratorDiagnosticCodes.PackagedAssetCorrupt)]
    [InlineData("r5-package", GeneratorDiagnosticCodes.IncompatibleFhirPackage)]
    [InlineData("r4b", GeneratorDiagnosticCodes.IncompatibleFhirPackage)]
    [InlineData("r5-model-policy", GeneratorDiagnosticCodes.IncompatibleModelPolicy)]
    [InlineData("package-bytes", GeneratorDiagnosticCodes.IncompatibleFhirPackage)]
    public async Task MixedReleaseInputsFailBeforeChangingOutput(string fault, string diagnostic)
    {
        Directory.CreateDirectory(_output);
        var sentinel = Path.Combine(_output, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        var (pipeline, _, options) = await Create(fault);
        options = options with { OutputPath = _output };
        if (fault == "r5-policy") options = options with { PrimitivePolicyPath = Path.Combine(Root, "CodeGen/Policy/primitive-generation-policy.json") };
        if (fault == "r5-package") options = options with { PackageId = "hl7.fhir.r5.core", PackageVersion = "5.0.0" };
        if (fault == "r4b") options = options with { FhirVersion = "4.3.0" };
        if (fault == "r5-model-policy") options = options with { ModelIrPolicyPaths = options.ModelIrPolicyPaths with { NamingPolicyPath = Path.Combine(Root, "CodeGen/Policy/r5-model-naming-policy.json") } };
        if (fault == "package-bytes")
        {
            var changed = Path.Combine(_output, "changed.tgz");
            var bytes = await File.ReadAllBytesAsync(options.PackagePath);
            bytes[4] ^= 1; // Valid gzip header timestamp change; definitions/identity remain unchanged.
            await File.WriteAllBytesAsync(changed, bytes);
            options = options with { PackagePath = changed };
            // Input remains outside the output being protected.
            var target = Path.Combine(_output, "destination");
            Directory.CreateDirectory(target);
            File.Move(sentinel, Path.Combine(target, "keep.txt"));
            sentinel = Path.Combine(target, "keep.txt");
            options = options with { OutputPath = target };
        }
        var result = await pipeline.GenerateAsync(options);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == diagnostic);
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        Assert.Equal([sentinel], Directory.GetFiles(options.OutputPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task R5PolicyAndAuxiliaryAreNotReusedForR4()
    {
        var r4 = FhirReleaseConfiguration.R4;
        var resolver = new ToolAssetResolver(Path.Combine(Root, "CodeGen/bin/Release/net9.0"));
        var overrides = new ToolAssetOverrides();
        Assert.EndsWith(Path.Combine("Contracts", "R4", "runtime-contract.json"), resolver.ResolveRuntimeContractPath(overrides, r4.FhirVersion));
        Assert.EndsWith(Path.Combine("CompilerSources", "R4", "SimpleQuantity.cs"), resolver.ResolveCompilerSourcePath(overrides, r4.FhirVersion));
        var contract = await new RuntimeContractLoader().LoadAsync(Path.Combine(Root, "CodeGen/Policy/R4/runtime-contract.json"));
        Assert.True(contract.IsSuccess, Describe(contract.Diagnostics));
        Assert.Equal("MyFhirSdk.R4.Types.SimpleQuantity", Assert.Single(contract.Value!.CompilerSources).ClrType);
        Assert.Contains("namespace MyFhirSdk.R4.Types", ModelCompilerSourceAsset.Read(contract: contract.Value).Source);
    }

    [Fact]
    public async Task DocumentedR4CommandsProduceCompleteModelsAndPrimitivesInOneTree()
    {
        var commands = (await File.ReadAllLinesAsync(Path.Combine(Root, "docs/fhir_versions/baselines/v4/README.md")))
            .Where(line => line.StartsWith("dotnet myfhir-codegen --mode ", StringComparison.Ordinal))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray()).ToArray();
        Assert.Equal(2, commands.Length);
        var modelCommand = commands.Single(args => args[1] == "model");
        var documentedRoot = Path.GetFullPath(modelCommand[Array.IndexOf(modelCommand, "--output") + 1], Root);
        var destination = Path.Combine(_output, "documented-tree");
        var (models, primitives, options) = await Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var parser = new GeneratorCommandLineParser(new ToolAssetResolver(Path.Combine(Root, "CodeGen/bin/Release/net9.0")));
        var cli = new GeneratorCli(output, error, parser, primitivePipeline: primitives, modelPipeline: models);
        foreach (var args in commands)
        {
            var outputIndex = Array.IndexOf(args, "--output") + 1;
            var relative = Path.GetRelativePath(documentedRoot, Path.GetFullPath(args[outputIndex], Root));
            Assert.False(relative.StartsWith("..", StringComparison.Ordinal));
            args[outputIndex] = Path.Combine(destination, relative);
            args[Array.IndexOf(args, "--input") + 1] = options.PackagePath;
            Assert.Equal(0, await cli.RunAsync(args));
        }
        var expected = await R4.Value;
        foreach (var artifact in expected.Models.Artifacts)
            Assert.Equal(CanonicalBytes(artifact.Content), await File.ReadAllBytesAsync(Path.Combine(destination, artifact.FileName)));
        foreach (var artifact in expected.Primitives.Artifacts)
            Assert.Equal(CanonicalBytes(artifact.Content), await File.ReadAllBytesAsync(Path.Combine(destination, "Generated/R4/Primitives", artifact.FileName)));
        Assert.Equal(expected.Models.Artifacts.Count + expected.Primitives.Artifacts.Count,
            Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length);

        static byte[] CanonicalBytes(string text) => System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace('\r', '\n'));
    }

    [Theory]
    [InlineData("4.0.1", "primitive")]
    [InlineData("4.0.1", "model")]
    [InlineData("5.0.0", "primitive")]
    [InlineData("5.0.0", "model")]
    public async Task ForeignAuxiliaryClrIdentityIsRejectedInBothModesWithoutChangingOutput(string version, string mode)
    {
        Directory.CreateDirectory(_output);
        var release = FhirReleaseConfiguration.ForVersion(version);
        var tool = new ToolAssetResolver(Path.Combine(Root, "CodeGen/bin/Release/net9.0"));
        var descriptor = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(tool.ResolveRuntimeContractPath(new(), version)))!;
        descriptor["compilerSources"]![0]!["clrType"] = version == "4.0.1"
            ? "MyFhirSdk.Types.SimpleQuantity" : "MyFhirSdk.R4.Types.SimpleQuantity";
        var descriptorPath = Path.Combine(_output, "mixed-contract.json");
        await File.WriteAllTextAsync(descriptorPath, descriptor.ToJsonString() + "\n");
        var loaded = await new RuntimeContractLoader().LoadAsync(descriptorPath);
        Assert.True(loaded.IsSuccess, Describe(loaded.Diagnostics));
        var contract = loaded.Value!;
        var references = new RuntimeReferenceService().Resolve(contract, tool.ResolveRuntimeReferencePaths(contract, new()),
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());
        Assert.True(references.IsSuccess, Describe(references.Diagnostics));
        var destination = Path.Combine(_output, "output");
        Directory.CreateDirectory(destination);
        var sentinel = Path.Combine(destination, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        var safety = new OutputSafetyContext(AppContext.BaseDirectory, [descriptorPath]);
        var validator = new RoslynCompilationValidator(references.Value!);
        var parser = new GeneratorCommandLineParser(tool);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new GeneratorCli(output, error, parser,
            primitivePipeline: new(safety, contract, validator),
            modelPipeline: new(safety, contract, validator, tool.ResolveCompilerSourcePath(new(), version)));
        var package = Path.Combine(Root, $"Tests/CodeGen/Fixtures/FhirPackages/{release.Release}/{release.PackageId}-{version}.tgz");
        Assert.Equal(2, await cli.RunAsync(["--mode", mode, "--input", package, "--output", destination,
            "--fhir-version", version, "--package-id", release.PackageId, "--package-version", version]));
        Assert.Contains(GeneratorDiagnosticCodes.PackagedAssetCorrupt, error.ToString());
        Assert.Contains("compiler-source-clr-type", error.ToString());
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        Assert.Equal([sentinel], Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RootSdkCannotAccidentallyCompileNewR4OrR5GeneratedTrees()
    {
        Directory.CreateDirectory(_output);
        foreach (var name in new[] { "MyFhirSdk.csproj", "Directory.Build.props", "Runtime/KernelCompileItems.props" })
        {
            var target = Path.Combine(_output, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(Root, name), target);
        }
        foreach (var release in new[] { "R4", "R5" })
        {
            var directory = Path.Combine(_output, "Generated", release);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "Probe.cs"), "// generated model compile owner probe");
        }
        await File.WriteAllTextAsync(Path.Combine(_output, "Handwritten.cs"), "// SDK-owned source");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _output, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var argument in new[] { "msbuild", "MyFhirSdk.csproj", "-getItem:Compile" }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        using var json = System.Text.Json.JsonDocument.Parse(await output);
        var compile = json.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!.Replace('\\', '/')).ToArray();
        Assert.Contains("Handwritten.cs", compile);
        Assert.DoesNotContain(compile, path => path.StartsWith("Generated/", StringComparison.Ordinal));
    }

    private static async Task<(ModelGenerationPipeline, PrimitiveGenerationPipeline, ModelGenerationOptions)> Create(string? fault = null)
    {
        var policyRoot = Path.Combine(Root, "CodeGen/Policy/R4");
        var contract = await new RuntimeContractLoader().LoadAsync(Path.Combine(Root, fault == "r5-contract" ? "CodeGen/Policy/runtime-contract.json" : "CodeGen/Policy/R4/runtime-contract.json"));
        Assert.True(contract.IsSuccess, Describe(contract.Diagnostics));
        var resolver = new ToolAssetResolver(AppContext.BaseDirectory);
        var references = new RuntimeReferenceService().Resolve(contract.Value!, resolver.ResolveRuntimeReferencePaths(contract.Value!, new()), RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());
        Assert.True(references.IsSuccess, Describe(references.Diagnostics));
        var validator = new RoslynCompilationValidator(references.Value!);
        var safety = new OutputSafetyContext(AppContext.BaseDirectory);
        var source = Path.Combine(Root, fault == "r5-auxiliary" ? "Models/R5/SimpleQuantity.cs" : "CodeGen/Assets/CompilerSources/R4/SimpleQuantity.cs");
        var policies = resolver.ResolveModelPolicies(new() { PolicyRoot = policyRoot }, null, "4.0.1");
        var options = new ModelGenerationOptions(Path.Combine(Root, "Tests/CodeGen/Fixtures/FhirPackages/R4/hl7.fhir.r4.core-4.0.1.tgz"), Path.GetTempPath(),
            "hl7.fhir.r4.core", "4.0.1", "4.0.1", policies.PrimitivePolicyPath, policies.OwnershipPolicyPath, policies.ModelIrPolicyPaths,
            policies.ValidationPolicyPath, [], ModelGenerationPipeline.DefaultCodeGenVersion);
        return (new(safety, contract.Value!, validator, source), new(safety, contract.Value!, validator), options);
    }

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) => string.Join("\n", diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    public void Dispose() { if (Directory.Exists(_output)) Directory.Delete(_output, true); }
}
