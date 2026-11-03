using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Policy;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Assets;

public sealed class ToolAssetResolver
{
    private readonly string _toolRoot;

    public ToolAssetResolver(string toolRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        _toolRoot = Path.GetFullPath(toolRoot);
    }

    public string ResolveRuntimeContractPath(ToolAssetOverrides overrides, string fhirVersion = "5.0.0")
    {
        ArgumentNullException.ThrowIfNull(overrides);
        return ResolveOverride(
            overrides.RuntimeContractPath,
            Path.Combine(_toolRoot, "Contracts", Generation.FhirReleaseConfiguration.ForVersion(fhirVersion).Release == "R5" ? "" : "R4", "runtime-contract.json"));
    }

    public IReadOnlyList<string> ResolveRuntimeReferencePaths(
        RuntimeContractView contract,
        ToolAssetOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(overrides);

        if (overrides.RuntimeReferences.Count > 0)
        {
            return overrides.RuntimeReferences
                .Select(Path.GetFullPath)
                .ToArray();
        }

        return
        [
            Path.Combine(
                _toolRoot,
                "Assets",
                "RuntimeReferences",
                contract.CompilerReference.TargetFramework,
                contract.CompilerReference.Assembly.Name + ".dll")
        ];
    }

    public string ResolveCompilerSourcePath(ToolAssetOverrides overrides, string fhirVersion = "5.0.0") =>
        ResolveOverride(overrides.CompilerSourcePath,
            Path.Combine(_toolRoot, "Assets", "CompilerSources", Generation.FhirReleaseConfiguration.ForVersion(fhirVersion).Release == "R5" ? "" : "R4", "SimpleQuantity.cs"));

    public ModelPolicyAssetPaths ResolveModelPolicies(
        ToolAssetOverrides overrides,
        string? primitivePolicyOverride, string fhirVersion = "5.0.0")
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var release = Generation.FhirReleaseConfiguration.ForVersion(fhirVersion);
        var policyRoot = string.IsNullOrWhiteSpace(overrides.PolicyRoot)
            ? Path.Combine(_toolRoot, "Policy", release.Release == "R5" ? "" : "R4")
            : Path.GetFullPath(overrides.PolicyRoot);

        string Policy(string name) => Path.Combine(policyRoot, name);
        return new ModelPolicyAssetPaths(
            ResolveOverride(
                primitivePolicyOverride,
                Policy("primitive-generation-policy.json")),
            Policy($"{release.Release.ToLowerInvariant()}-model-ownership-policy.json"),
            new ModelIrPolicyPaths(
                Policy($"{release.Release.ToLowerInvariant()}-model-naming-policy.json"),
                Policy($"{release.Release.ToLowerInvariant()}-backbone-policy.json"),
                Policy($"{release.Release.ToLowerInvariant()}-choice-open-type-policy.json")),
            Policy($"{release.Release.ToLowerInvariant()}-validation-capability-policy.json"));
    }

    public OutputSafetyContext CreateOutputSafetyContext(
        string inputPath,
        string runtimeContractPath,
        IEnumerable<string> runtimeReferencePaths,
        IEnumerable<string> policyPaths) =>
        new(
            _toolRoot,
            new[] { inputPath, runtimeContractPath }
                .Concat(runtimeReferencePaths)
                .Concat(policyPaths));

    private static string ResolveOverride(string? value, string defaultPath) =>
        string.IsNullOrWhiteSpace(value)
            ? Path.GetFullPath(defaultPath)
            : Path.GetFullPath(value);
}

public sealed record ModelPolicyAssetPaths(
    string PrimitivePolicyPath,
    string OwnershipPolicyPath,
    ModelIrPolicyPaths ModelIrPolicyPaths,
    string ValidationPolicyPath)
{
    public IReadOnlyList<string> AllPaths =>
    [
        PrimitivePolicyPath,
        OwnershipPolicyPath,
        ModelIrPolicyPaths.NamingPolicyPath,
        ModelIrPolicyPaths.BackbonePolicyPath,
        ModelIrPolicyPaths.ChoicePolicyPath,
        ValidationPolicyPath
    ];
}
