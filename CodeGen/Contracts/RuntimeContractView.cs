using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Contracts;

public sealed class RuntimeContractView
{
    internal RuntimeContractView(
        int schemaVersion,
        string contractVersion,
        string targetFramework,
        RuntimeAssemblyIdentity runtimeAssembly,
        RuntimeCompatibility compatibility,
        IEnumerable<RuntimeSymbol> symbols,
        IEnumerable<RuntimeDeclaredSlot> declaredSlots,
        RuntimeCompilerReference compilerReference,
        string descriptorSha256)
    {
        SchemaVersion = schemaVersion;
        ContractVersion = contractVersion;
        TargetFramework = targetFramework;
        RuntimeAssembly = runtimeAssembly;
        Compatibility = compatibility;
        Symbols = new ReadOnlyCollection<RuntimeSymbol>(symbols.ToArray());
        DeclaredSlots = new ReadOnlyCollection<RuntimeDeclaredSlot>(declaredSlots.ToArray());
        CompilerReference = compilerReference;
        DescriptorSha256 = descriptorSha256;
    }

    public int SchemaVersion { get; }
    public string ContractVersion { get; }
    public string TargetFramework { get; }
    public RuntimeAssemblyIdentity RuntimeAssembly { get; }
    public RuntimeCompatibility Compatibility { get; }
    public IReadOnlyList<RuntimeSymbol> Symbols { get; }
    public IReadOnlyList<RuntimeDeclaredSlot> DeclaredSlots { get; }
    public RuntimeCompilerReference CompilerReference { get; }
    public string DescriptorSha256 { get; }
}

public sealed record RuntimeAssemblyIdentity(
    string Name,
    string Version,
    string PublicKeyToken);

public sealed record RuntimeCompatibility(
    string ToolVersion,
    string CodeGenVersion,
    RuntimeFhirPackageIdentity FhirPackage,
    RuntimePolicyIdentity PrimitivePolicy,
    IReadOnlyList<RuntimeNamedAssetIdentity> ModelPolicies);

public sealed record RuntimeFhirPackageIdentity(
    string Id,
    string Version,
    string FhirVersion);

public sealed record RuntimePolicyIdentity(string Version, string Sha256);

public sealed record RuntimeNamedAssetIdentity(string Name, string Sha256);

public sealed record RuntimeSymbol(
    string ClrType,
    string Role,
    string Kind,
    string? BaseClrType,
    bool IsAbstract,
    bool IsSealed,
    int GenericArity,
    IReadOnlyList<string> Interfaces);

public sealed record RuntimeDeclaredSlot(
    string DeclaringClrType,
    string ClrPropertyName,
    string PropertyClrType,
    string ElementClrType,
    bool IsCollection,
    bool IsNullable,
    string Role);

public sealed record RuntimeCompilerReference(
    string LogicalName,
    string TargetFramework,
    RuntimeAssemblyIdentity Assembly,
    string Sha256);
