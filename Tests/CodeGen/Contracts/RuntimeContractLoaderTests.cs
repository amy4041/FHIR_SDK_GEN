using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Contracts;

public sealed class RuntimeContractLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "MyFhirSdk-RuntimeContractTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_RepositoryDescriptor_ReturnsImmutableDeterministicView()
    {
        var path = GetRepositoryDescriptorPath();
        var loader = new RuntimeContractLoader();

        var first = await loader.LoadAsync(path);
        var second = await loader.LoadAsync(path);

        Assert.True(first.IsSuccess, Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, Describe(second.Diagnostics));
        var view = Assert.IsType<RuntimeContractView>(first.Value);
        var secondView = Assert.IsType<RuntimeContractView>(second.Value);
        Assert.Equal(1, view.SchemaVersion);
        Assert.Equal("phase-a-v1+c4-primitives-v1", view.ContractVersion);
        Assert.Equal("net9.0", view.TargetFramework);
        Assert.Equal(13, view.Symbols.Count);
        Assert.Equal(3, view.DeclaredSlots.Count);
        Assert.Equal(
            "4923075ae0eb4ac88fefe6292b68a893b2e55d25e79c1746dfffa0bc266ce210",
            view.DescriptorSha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant(),
            view.DescriptorSha256);
        Assert.Equal(Snapshot(view), Snapshot(secondView));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeSymbol>)view.Symbols).Add(view.Symbols[0]));
    }

    [Fact]
    public async Task LoadAsync_DescriptorContainsOnlyApprovedRuntimeSymbols()
    {
        var result = await new RuntimeContractLoader().LoadAsync(GetRepositoryDescriptorPath());

        var view = Assert.IsType<RuntimeContractView>(result.Value);
        Assert.All(view.Symbols, symbol => Assert.StartsWith("MyFhirSdk.Core.", symbol.ClrType));
        Assert.DoesNotContain(view.Symbols, symbol =>
            symbol.ClrType.StartsWith("MyFhirSdk.Types.", StringComparison.Ordinal) ||
            symbol.ClrType.StartsWith("MyFhirSdk.Resources.", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Symbols, symbol => symbol.ClrType.Contains("SimpleQuantity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsFsg0100()
    {
        var result = await new RuntimeContractLoader().LoadAsync(Path.Combine(_directory, "missing.json"));

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeContractReadFailure);
    }

    [Fact]
    public async Task LoadAsync_MalformedJson_ReturnsFsg0101()
    {
        var result = await LoadTextAsync("{");

        AssertFailure(result, GeneratorDiagnosticCodes.InvalidRuntimeContractJson);
    }

    [Fact]
    public async Task LoadAsync_UnknownProperty_ReturnsFsg0103()
    {
        var root = ReadDescriptor();
        root["unknownField"] = true;

        var result = await LoadNodeAsync(root);

        AssertFailure(result, GeneratorDiagnosticCodes.InvalidRuntimeContract);
    }

    [Fact]
    public async Task LoadAsync_DuplicateJsonProperty_ReturnsFsg0104()
    {
        var json = await File.ReadAllTextAsync(GetRepositoryDescriptorPath());
        json = json.Replace(
            "  \"schemaVersion\": 1,",
            "  \"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);

        var result = await LoadTextAsync(json);

        AssertFailure(result, GeneratorDiagnosticCodes.DuplicateRuntimeContractEntry);
        Assert.Contains("$.schemaVersion", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_UnsupportedSchema_ReturnsFsg0102()
    {
        var root = ReadDescriptor();
        root["schemaVersion"] = 2;

        var result = await LoadNodeAsync(root);

        AssertFailure(result, GeneratorDiagnosticCodes.UnsupportedRuntimeContractSchema);
    }

    [Fact]
    public async Task LoadAsync_UnknownSymbolRole_ReturnsFsg0105()
    {
        var root = ReadDescriptor();
        root["symbols"]![0]!["role"] = "future-role";

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.UnknownRuntimeContractRole);
    }

    [Fact]
    public async Task LoadAsync_MissingRequiredDatatypeRole_FailsFast()
    {
        var root = ReadDescriptor();
        var symbols = root["symbols"]!.AsArray();
        var datatype = symbols.Single(symbol =>
            symbol!["role"]!.GetValue<string>() == RuntimeContractRoles.DatatypeFoundation);
        symbols.Remove(datatype);

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.InvalidRuntimeContract &&
            diagnostic.Message.Contains("datatype-foundation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_DuplicateAndOutOfOrderSymbols_ReturnsStableDiagnostics()
    {
        var root = ReadDescriptor();
        var symbols = root["symbols"]!.AsArray();
        symbols.Insert(0, symbols[0]!.DeepClone());

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.DuplicateRuntimeContractEntry);
        Assert.Equal(
            result.Diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal),
            result.Diagnostics);
    }

    [Fact]
    public async Task LoadAsync_InvalidReferenceCrossLink_ReturnsFsg0103()
    {
        var root = ReadDescriptor();
        root["compilerReference"]!["assembly"]!["version"] = "2.0.0.0";

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.InvalidRuntimeContract &&
            diagnostic.Message.Contains("does not match runtimeAssembly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_WrongRoleBase_ReturnsFsg0103()
    {
        var root = ReadDescriptor();
        var datatype = root["symbols"]!.AsArray().Single(symbol =>
            symbol!["role"]!.GetValue<string>() == RuntimeContractRoles.DatatypeFoundation);
        datatype!["baseClrType"] = "MyFhirSdk.Core.Resource";

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.InvalidRuntimeContract &&
            diagnostic.Message.Contains("datatype-foundation", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("base CLR type", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_WrongPrimitiveGenericArity_ReturnsFsg0103()
    {
        var root = ReadDescriptor();
        var primitive = root["symbols"]!.AsArray().Single(symbol =>
            symbol!["role"]!.GetValue<string>() == RuntimeContractRoles.PrimitiveWrapperBase);
        primitive!["genericArity"] = 0;

        var result = await LoadNodeAsync(root);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == GeneratorDiagnosticCodes.InvalidRuntimeContract &&
            diagnostic.Message.Contains("primitive-wrapper-base", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("generic arity", StringComparison.Ordinal));
    }

    private static string GetRepositoryDescriptorPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        "runtime-contract.json");

    private static JsonObject ReadDescriptor() =>
        JsonNode.Parse(File.ReadAllText(GetRepositoryDescriptorPath()))!.AsObject();

    private async Task<GenerationResult<RuntimeContractView?>> LoadNodeAsync(JsonObject root) =>
        await LoadTextAsync(
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n') +
            "\n");

    private async Task<GenerationResult<RuntimeContractView?>> LoadTextAsync(string text)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, text);
        return await new RuntimeContractLoader().LoadAsync(path);
    }

    private static void AssertFailure(
        GenerationResult<RuntimeContractView?> result,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    private static string[] Snapshot(RuntimeContractView view) =>
    [
        $"schema|{view.SchemaVersion}|{view.ContractVersion}|{view.TargetFramework}|{view.DescriptorSha256}",
        $"assembly|{view.RuntimeAssembly.Name}|{view.RuntimeAssembly.Version}|{view.RuntimeAssembly.PublicKeyToken}",
        $"compatibility|{view.Compatibility.ToolVersion}|{view.Compatibility.CodeGenVersion}|{view.Compatibility.FhirPackage.Id}|{view.Compatibility.FhirPackage.Version}|{view.Compatibility.FhirPackage.FhirVersion}|{view.Compatibility.PrimitivePolicy.Version}|{view.Compatibility.PrimitivePolicy.Sha256}",
        .. view.Compatibility.ModelPolicies.Select(policy =>
            $"policy|{policy.Name}|{policy.Sha256}"),
        .. view.Symbols.Select(symbol =>
            $"symbol|{symbol.ClrType}|{symbol.Role}|{symbol.Kind}|{symbol.BaseClrType}|{symbol.IsAbstract}|{symbol.IsSealed}|{symbol.GenericArity}|{string.Join(',', symbol.Interfaces)}"),
        .. view.DeclaredSlots.Select(slot =>
            $"slot|{slot.DeclaringClrType}|{slot.ClrPropertyName}|{slot.PropertyClrType}|{slot.ElementClrType}|{slot.IsCollection}|{slot.IsNullable}|{slot.Role}"),
        $"reference|{view.CompilerReference.LogicalName}|{view.CompilerReference.TargetFramework}|{view.CompilerReference.Assembly.Name}|{view.CompilerReference.Assembly.Version}|{view.CompilerReference.Assembly.PublicKeyToken}|{view.CompilerReference.Sha256}"
    ];

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"[{diagnostic.Code}] {diagnostic.Message}"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
