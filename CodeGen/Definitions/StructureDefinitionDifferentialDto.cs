using System.Text.Json.Serialization;

namespace MyFhirSdk.CodeGen.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed class StructureDefinitionDifferentialDto
{
    [JsonPropertyName("element")]
    public List<ElementDefinitionDto>? Elements { get; init; }
}
