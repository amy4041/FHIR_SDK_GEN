using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class RuntimePhysicalOwnershipTests
{
    private static readonly string[] KernelNames =
    [
        "BackboneElement", "BackboneType", "Base", "DataType", "DomainResource", "Element",
        "Extension", "FhirObject", "IFhirExtensionValue", "IPrimitiveValueAccessor", "Meta",
        "Narrative", "PrimitiveType`1", "Resource"
    ];

    [Fact]
    public void ProductionPeOwnsExactlyTheApprovedKernelAndHasNoSdkDependencies()
    {
        var runtime = typeof(FhirObject).Assembly;
        var sdk = typeof(FhirString).Assembly;
        Assert.Equal("MyFhirSdk.Runtime", runtime.GetName().Name);
        Assert.NotSame(runtime, sdk);
        Assert.Equal(KernelNames, runtime.GetExportedTypes().Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(runtime.GetExportedTypes(), type => Assert.Equal("MyFhirSdk.Core", type.Namespace));
        Assert.DoesNotContain(runtime.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "InternalsVisibleToAttribute");
        using var runtimeStream = File.OpenRead(runtime.Location);
        using var runtimePe = new PEReader(runtimeStream);
        var metadata = runtimePe.GetMetadataReader();
        Assert.All(metadata.AssemblyReferences, handle =>
            Assert.StartsWith("System.", metadata.GetString(metadata.GetAssemblyReference(handle).Name)));
        using var sdkStream = File.OpenRead(sdk.Location);
        using var sdkPe = new PEReader(sdkStream);
        var sdkMetadata = sdkPe.GetMetadataReader();
        Assert.Contains(sdkMetadata.AssemblyReferences, handle =>
            sdkMetadata.GetString(sdkMetadata.GetAssemblyReference(handle).Name) == "MyFhirSdk.Runtime");
        Assert.DoesNotContain(sdkMetadata.TypeDefinitions, handle =>
        {
            var type = sdkMetadata.GetTypeDefinition(handle);
            return sdkMetadata.GetString(type.Namespace) == "MyFhirSdk.Core"
                && KernelNames.Contains(sdkMetadata.GetString(type.Name));
        });
        Assert.Same(sdk, typeof(FhirSdkException).Assembly);
        Assert.DoesNotContain(typeof(MyFhirSdk.CodeGen.Cli.GeneratorCli).Assembly.GetReferencedAssemblies(),
            identity => identity.Name is "MyFhirSdk.Runtime" or "MyFhirSdk");
    }

    [Fact]
    public async Task EvaluatedProductionCompileItemsHaveOneOwnerAndOneWayProjectReferences()
    {
        var root = RepositoryRoot();
        using var runtime = await Evaluate(Path.Combine(root, "Runtime", "MyFhirSdk.Runtime.csproj"));
        using var sdk = await Evaluate(Path.Combine(root, "MyFhirSdk.csproj"));
        var runtimeItems = runtime.RootElement.GetProperty("Items");
        var sdkItems = sdk.RootElement.GetProperty("Items");
        Assert.Empty(runtimeItems.GetProperty("ProjectReference").EnumerateArray());
        var references = sdkItems.GetProperty("ProjectReference").EnumerateArray().ToArray();
        Assert.Single(references);
        Assert.Equal(Path.Combine(root, "Runtime", "MyFhirSdk.Runtime.csproj"), references[0].GetProperty("FullPath").GetString());
        var runtimeSources = Paths(runtimeItems, "Compile");
        var sdkSources = Paths(sdkItems, "Compile");
        var expected = KernelNames.Select(name => Path.Combine(root, "core", name.Replace("`1", "") + ".cs")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(expected.SetEquals(runtimeSources));
        Assert.Empty(runtimeSources.Intersect(sdkSources, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(runtime.RootElement.GetProperty("Properties").GetProperty("TargetFramework").GetString(),
            sdk.RootElement.GetProperty("Properties").GetProperty("TargetFramework").GetString());
    }

    private static HashSet<string> Paths(JsonElement items, string name) => items.GetProperty(name).EnumerateArray()
        .Select(item => item.GetProperty("FullPath").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task<JsonDocument> Evaluate(string project)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = RepositoryRoot() };
        foreach (var argument in new[] { "msbuild", project, "-p:Configuration=Release", "-getItem:Compile,ProjectReference", "-getProperty:TargetFramework" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        Assert.True(process.ExitCode == 0, await errorTask);
        return JsonDocument.Parse(output);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MyFhirSdk.sln"))) return directory.FullName;
        throw new InvalidOperationException("Physical ownership tests require the source checkout.");
    }
}
