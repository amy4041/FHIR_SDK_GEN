using System.Text.Json;
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

    [JsonPropertyName("slicing")]
    public JsonElement? Slicing { get; init; }

    [JsonPropertyName("base")]
    public ElementDefinitionBaseDto? Base { get; init; }

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

    [JsonPropertyName("constraint")]
    public List<ElementConstraintDto>? Constraints { get; init; }

    [JsonPropertyName("binding")]
    public ElementBindingDto? Binding { get; init; }

    [JsonPropertyName("mustSupport")]
    public bool? MustSupport { get; init; }

    [JsonPropertyName("isModifier")]
    public bool? IsModifier { get; init; }

    [JsonPropertyName("isModifierReason")]
    public string? IsModifierReason { get; init; }

    [JsonPropertyName("isSummary")]
    public bool? IsSummary { get; init; }

    [JsonPropertyName("condition")]
    public List<string>? Conditions { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
