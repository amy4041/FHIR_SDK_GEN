using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class StructureDefinitionSnapshotDto
{
    [JsonPropertyName("element")]
    public List<ElementDefinitionDto>? Elements { get; init; }
}
