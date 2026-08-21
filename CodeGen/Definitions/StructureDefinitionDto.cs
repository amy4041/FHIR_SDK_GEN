using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class StructureDefinitionDto
{
    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("abstract")]
    public bool? IsAbstract { get; init; }

    [JsonPropertyName("baseDefinition")]
    public string? BaseDefinition { get; init; }

    [JsonPropertyName("derivation")]
    public string? Derivation { get; init; }

    [JsonPropertyName("snapshot")]
    public StructureDefinitionSnapshotDto? Snapshot { get; init; }

    [JsonPropertyName("differential")]
    public StructureDefinitionDifferentialDto? Differential { get; init; }
}
