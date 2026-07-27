using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class ElementTypeDto
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("profile")]
    public List<string>? Profiles { get; init; }

    [JsonPropertyName("targetProfile")]
    public List<string>? TargetProfiles { get; init; }
}
