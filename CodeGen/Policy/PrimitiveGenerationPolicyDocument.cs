using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Policy;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PrimitiveGenerationPolicyDocument
{
    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("policyVersion")]
    public string? PolicyVersion { get; init; }

    [JsonPropertyName("fhirVersion")]
    public string? FhirVersion { get; init; }

    [JsonPropertyName("runtimeContractVersion")]
    public string? RuntimeContractVersion { get; init; }

    [JsonPropertyName("primitiveNamespace")]
    public string? PrimitiveNamespace { get; init; }

    [JsonPropertyName("primitives")]
    public IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?>? Primitives { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PrimitiveGenerationPolicyEntryDocument
{
    [JsonPropertyName("fhirTypeName")]
    public string? FhirTypeName { get; init; }

    [JsonPropertyName("canonical")]
    public string? Canonical { get; init; }

    [JsonPropertyName("fhirVersion")]
    public string? FhirVersion { get; init; }

    [JsonPropertyName("wrapperName")]
    public string? WrapperName { get; init; }

    [JsonPropertyName("clrValueType")]
    public string? ClrValueType { get; init; }

    [JsonPropertyName("jsonToken")]
    public string? JsonToken { get; init; }

    [JsonPropertyName("codecKey")]
    public string? CodecKey { get; init; }

    [JsonPropertyName("validatorKey")]
    public string? ValidatorKey { get; init; }

    [JsonPropertyName("preserveLiteral")]
    public bool? PreserveLiteral { get; init; }

    [JsonPropertyName("literalConstructor")]
    public bool? LiteralConstructor { get; init; }

    [JsonPropertyName("literalPropertyName")]
    public string? LiteralPropertyName { get; init; }

    [JsonPropertyName("supportStatus")]
    public string? SupportStatus { get; init; }

    [JsonPropertyName("unsupportedReason")]
    public string? UnsupportedReason { get; init; }

    [JsonPropertyName("toStringBehavior")]
    public string? ToStringBehavior { get; init; }

    [JsonPropertyName("publicConstants")]
    public IReadOnlyList<PrimitivePublicConstantDocument?>? PublicConstants { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PrimitivePublicConstantDocument
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("clrType")]
    public string? ClrType { get; init; }

    [JsonPropertyName("value")]
    public long? Value { get; init; }
}
