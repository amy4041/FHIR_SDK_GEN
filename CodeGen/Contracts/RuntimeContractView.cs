using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Contracts;

public sealed class RuntimeContractView
{
    private readonly IReadOnlyDictionary<string, RuntimeSymbol> _symbolsByClrType;

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
        _symbolsByClrType = new ReadOnlyDictionary<string, RuntimeSymbol>(
            Symbols.ToDictionary(symbol => symbol.ClrType, StringComparer.Ordinal));
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

    public bool TryGetSymbol(string clrType, out RuntimeSymbol? symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clrType);
        return _symbolsByClrType.TryGetValue(clrType, out symbol);
    }

    public RuntimeSymbol GetRequiredRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return Symbols.Single(symbol => string.Equals(
            symbol.Role,
            role,
            StringComparison.Ordinal));
    }

    public bool IsAssignableTo(string clrType, string baseClrType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clrType);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseClrType);

        var current = clrType;
        while (_symbolsByClrType.TryGetValue(current, out var symbol))
        {
            if (string.Equals(current, baseClrType, StringComparison.Ordinal))
            {
                return true;
            }
            if (symbol.BaseClrType is null)
            {
                return false;
            }
            current = symbol.BaseClrType;
        }
        return string.Equals(current, baseClrType, StringComparison.Ordinal);
    }
}

public sealed record RuntimeAssemblyIdentity(
    string Name,
    string Version,
    string PublicKeyToken);

public sealed record RuntimeCompatibility(
    int SchemaVersion,
    string VersionPolicy,
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
