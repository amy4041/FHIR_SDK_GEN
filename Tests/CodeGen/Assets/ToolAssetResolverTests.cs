using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Contracts;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Assets;

[Collection(ProcessStateCollection.Name)]
public sealed class ToolAssetResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MyFhirSdk-ToolAssetResolverTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PackageDefaults_AreResolvedFromToolRoot_NotCurrentDirectory()
    {
        var toolRoot = Path.Combine(_root, "installed-tool");
        var unrelatedDirectory = Path.Combine(_root, "unrelated", "empty");
        Directory.CreateDirectory(unrelatedDirectory);
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(unrelatedDirectory);
            var resolver = new ToolAssetResolver(toolRoot);

            var contract = resolver.ResolveRuntimeContractPath(new ToolAssetOverrides());
            var policies = resolver.ResolveModelPolicies(new ToolAssetOverrides(), null);

            Assert.Equal(
                Path.Combine(toolRoot, "Contracts", "runtime-contract.json"),
                contract);
            Assert.Equal(
                Path.Combine(toolRoot, "Policy", "primitive-generation-policy.json"),
                policies.PrimitivePolicyPath);
            Assert.All(policies.AllPaths, path =>
                Assert.StartsWith(Path.Combine(toolRoot, "Policy"), path));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void ExplicitOverrides_WinWithoutFallingBackToPackageAssets()
    {
        var resolver = new ToolAssetResolver(Path.Combine(_root, "tool"));
        var policyRoot = Path.Combine(_root, "custom-policy");
        var contractPath = Path.Combine(_root, "contract.json");
        var referenceZ = Path.Combine(_root, "z.dll");
        var referenceA = Path.Combine(_root, "a.dll");
        var overrides = new ToolAssetOverrides(
            policyRoot,
            contractPath,
            [referenceZ, referenceA]);
        var contract = CreateContractView();

        var policies = resolver.ResolveModelPolicies(overrides, null);
        var references = resolver.ResolveRuntimeReferencePaths(contract, overrides);

        Assert.Equal(Path.GetFullPath(contractPath),
            resolver.ResolveRuntimeContractPath(overrides));
        Assert.All(policies.AllPaths, path =>
            Assert.StartsWith(Path.GetFullPath(policyRoot), path));
        Assert.Equal(
            [Path.GetFullPath(referenceZ), Path.GetFullPath(referenceA)],
            references);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RuntimeContractView CreateContractView()
    {
        var result = new RuntimeContractLoader().LoadAsync(Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "runtime-contract.json"))
            .GetAwaiter()
            .GetResult();
        return Assert.IsType<RuntimeContractView>(result.Value);
    }
}
