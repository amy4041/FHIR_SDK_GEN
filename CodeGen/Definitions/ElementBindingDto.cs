using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class ElementBindingDto
{
    [JsonPropertyName("strength")]
    public string? Strength { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("valueSet")]
    public string? ValueSet { get; init; }
}
