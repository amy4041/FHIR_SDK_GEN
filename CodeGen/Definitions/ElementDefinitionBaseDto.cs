using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class ElementDefinitionBaseDto
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("min")]
    public int? Min { get; init; }

    [JsonPropertyName("max")]
    public string? Max { get; init; }
}
