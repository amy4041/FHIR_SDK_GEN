using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class ElementConstraintDto
{
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("human")]
    public string? Human { get; init; }

    [JsonPropertyName("expression")]
    public string? Expression { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}
