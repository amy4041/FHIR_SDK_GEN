using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Loading;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
internal sealed class DefinitionPackageDocumentDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("fhirVersions")]
    public List<string>? FhirVersions { get; init; }
}
