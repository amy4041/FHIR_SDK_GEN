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

    public string ResolveRuntimeContractPath(ToolAssetOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        return ResolveOverride(
            overrides.RuntimeContractPath,
            Path.Combine(_toolRoot, "Contracts", "runtime-contract.json"));
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

    public ModelPolicyAssetPaths ResolveModelPolicies(
        ToolAssetOverrides overrides,
        string? primitivePolicyOverride)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var policyRoot = string.IsNullOrWhiteSpace(overrides.PolicyRoot)
            ? Path.Combine(_toolRoot, "Policy")
            : Path.GetFullPath(overrides.PolicyRoot);

        string Policy(string name) => Path.Combine(policyRoot, name);
        return new ModelPolicyAssetPaths(
            ResolveOverride(
                primitivePolicyOverride,
                Policy("primitive-generation-policy.json")),
            Policy("r5-model-ownership-policy.json"),
            new ModelIrPolicyPaths(
                Policy("r5-model-naming-policy.json"),
                Policy("r5-backbone-policy.json"),
                Policy("r5-choice-open-type-policy.json")),
            Policy("r5-validation-capability-policy.json"));
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
