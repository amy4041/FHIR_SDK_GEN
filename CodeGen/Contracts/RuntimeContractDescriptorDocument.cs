using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Contracts;

public sealed class RuntimeContractDescriptorDocument
{
    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("contractVersion")]
    public string? ContractVersion { get; init; }

    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; init; }

    [JsonPropertyName("runtimeAssembly")]
    public RuntimeAssemblyIdentityDocument? RuntimeAssembly { get; init; }

    [JsonPropertyName("compatibility")]
    public RuntimeCompatibilityDocument? Compatibility { get; init; }

    [JsonPropertyName("symbols")]
    public List<RuntimeSymbolDocument>? Symbols { get; init; }

    [JsonPropertyName("declaredSlots")]
    public List<RuntimeDeclaredSlotDocument>? DeclaredSlots { get; init; }

    [JsonPropertyName("compilerReference")]
    public RuntimeCompilerReferenceDocument? CompilerReference { get; init; }
}

public sealed class RuntimeAssemblyIdentityDocument
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("publicKeyToken")]
    public string? PublicKeyToken { get; init; }
}

public sealed class RuntimeCompatibilityDocument
{
    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    [JsonPropertyName("codeGenVersion")]
    public string? CodeGenVersion { get; init; }

    [JsonPropertyName("fhirPackage")]
    public RuntimeFhirPackageIdentityDocument? FhirPackage { get; init; }

    [JsonPropertyName("primitivePolicy")]
    public RuntimePolicyIdentityDocument? PrimitivePolicy { get; init; }

    [JsonPropertyName("modelPolicies")]
    public List<RuntimeNamedAssetIdentityDocument>? ModelPolicies { get; init; }
}

public sealed class RuntimeFhirPackageIdentityDocument
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("fhirVersion")]
    public string? FhirVersion { get; init; }
}

public sealed class RuntimePolicyIdentityDocument
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }
}

public sealed class RuntimeNamedAssetIdentityDocument
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }
}

public sealed class RuntimeSymbolDocument
{
    [JsonPropertyName("clrType")]
    public string? ClrType { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("baseClrType")]
    public string? BaseClrType { get; init; }

    [JsonPropertyName("abstract")]
    public bool? IsAbstract { get; init; }

    [JsonPropertyName("sealed")]
    public bool? IsSealed { get; init; }

    [JsonPropertyName("genericArity")]
    public int? GenericArity { get; init; }

    [JsonPropertyName("interfaces")]
    public List<string>? Interfaces { get; init; }
}

public sealed class RuntimeDeclaredSlotDocument
{
    [JsonPropertyName("declaringClrType")]
    public string? DeclaringClrType { get; init; }

    [JsonPropertyName("clrPropertyName")]
    public string? ClrPropertyName { get; init; }

    [JsonPropertyName("propertyClrType")]
    public string? PropertyClrType { get; init; }

    [JsonPropertyName("elementClrType")]
    public string? ElementClrType { get; init; }

    [JsonPropertyName("collection")]
    public bool? IsCollection { get; init; }

    [JsonPropertyName("nullable")]
    public bool? IsNullable { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

public sealed class RuntimeCompilerReferenceDocument
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; init; }

    [JsonPropertyName("assembly")]
    public RuntimeAssemblyIdentityDocument? Assembly { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }
}
