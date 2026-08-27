namespace MyFhirSdk.CodeGen.Policy;

public static class PrimitiveGenerationPolicyDefaults
{
    public static string GetPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        "primitive-generation-policy.json");
}
