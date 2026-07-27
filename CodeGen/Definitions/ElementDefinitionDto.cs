using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class ElementDefinitionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("sliceName")]
    public string? SliceName { get; init; }

    [JsonPropertyName("min")]
    public int? Min { get; init; }

    [JsonPropertyName("max")]
    public string? Max { get; init; }

    [JsonPropertyName("contentReference")]
    public string? ContentReference { get; init; }

    [JsonPropertyName("type")]
    public List<ElementTypeDto>? Types { get; init; }

    [JsonPropertyName("short")]
    public string? Short { get; init; }

    [JsonPropertyName("definition")]
    public string? Definition { get; init; }
}
