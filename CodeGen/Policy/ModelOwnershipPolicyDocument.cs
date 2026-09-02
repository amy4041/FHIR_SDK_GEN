using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class ModelOwnershipPolicyDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("fhirVersion")]
    public string? FhirVersion { get; init; }

    [JsonPropertyName("externalDefinitionNodes")]
    public List<ExternalDefinitionPolicyNode>? ExternalDefinitionNodes { get; init; }
}

public sealed class ExternalDefinitionPolicyNode
{
    [JsonPropertyName("fhirType")]
    public string? FhirType { get; init; }

    [JsonPropertyName("canonical")]
    public string? Canonical { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("abstract")]
    public bool IsAbstract { get; init; }

    [JsonPropertyName("baseCanonical")]
    public string? BaseCanonical { get; init; }

    [JsonPropertyName("clrType")]
    public string? ClrType { get; init; }

    [JsonPropertyName("generationDisposition")]
    public string? GenerationDisposition { get; init; }
}
