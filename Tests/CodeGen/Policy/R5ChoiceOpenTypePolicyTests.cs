using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Types;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class R5ChoiceOpenTypePolicyTests
{
    private static readonly Lazy<IReadOnlyList<OfficialDefinition>> Definitions =
        new(ReadOfficialDefinitions);

    private readonly CSharpNameConverter _converter = new();

    [Fact]
    public void ChoiceRepresentationsAreExplicitAndPreserveCurrentApiShape()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var root = policy.RootElement;
        var ordinary = root.GetProperty("ordinaryChoiceRepresentation");
        var open = root.GetProperty("openTypeRepresentation");
        var validation = root.GetProperty("cardinalityAndValidation");

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("5.0.0", root.GetProperty("fhirVersion").GetString());
        Assert.Equal(
            "one-nullable-property-per-alternative",
            ordinary.GetProperty("publicShape").GetString());
        Assert.False(ordinary.GetProperty("aggregatePropertyAllowed").GetBoolean());
        Assert.True(ordinary.GetProperty("allAlternativePropertiesNullable").GetBoolean());
        Assert.False(ordinary.GetProperty("setterClearsOtherAlternatives").GetBoolean());
        Assert.Equal(
            "one-nullable-polymorphic-property",
            open.GetProperty("publicShape").GetString());
        Assert.Equal(
            "MyFhirSdk.Core.DataType",
            open.GetProperty("generatedClrType").GetString());
        Assert.False(open.GetProperty("clrTypeNameHeuristicsAllowed").GetBoolean());
        Assert.True(open.GetProperty("allAlternativesMustRemainInIr").GetBoolean());
        Assert.Equal(
            "exactly-one-alternative",
            validation.GetProperty("ordinaryMinOne").GetString());
        Assert.Equal(
            "at-most-one-alternative",
            validation.GetProperty("ordinaryMinZero").GetString());

        Assert.Equal(typeof(FhirBoolean), GetPropertyType(typeof(Patient), "DeceasedBoolean"));
        Assert.Equal(typeof(FhirDateTime), GetPropertyType(typeof(Patient), "DeceasedDateTime"));
        Assert.Equal(typeof(FhirDateTime), GetPropertyType(typeof(ClaimEvent), "WhenDateTime"));
        Assert.Equal(typeof(Period), GetPropertyType(typeof(ClaimEvent), "WhenPeriod"));
        Assert.Null(typeof(Patient).GetProperty("Deceased", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void OfficialChoiceInventoryMatchesApprovedR5Shape()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var approved = policy.RootElement.GetProperty("approvedR5Inventory");
        var choices = GetGeneratedScopeChoices();
        var openIds = GetOpenTypeElementIds(policy);
        var openChoices = choices
            .Where(choice => openIds.Contains(choice.Element.Id))
            .ToArray();
        var ordinaryChoices = choices
            .Where(choice => !openIds.Contains(choice.Element.Id))
            .ToArray();

        Assert.Equal(
            approved.GetProperty("generatedScopeChoiceElementCount").GetInt32(),
            choices.Count);
        Assert.Equal(
            approved.GetProperty("generatedScopeChoiceAlternativeCount").GetInt32(),
            choices.Sum(choice => choice.Element.TypeCodes.Count));
        Assert.Equal(
            approved.GetProperty("ordinaryChoiceElementCount").GetInt32(),
            ordinaryChoices.Length);
        Assert.Equal(
            approved.GetProperty("ordinaryChoiceAlternativeCount").GetInt32(),
            ordinaryChoices.Sum(choice => choice.Element.TypeCodes.Count));
        Assert.Equal(
            approved.GetProperty("openTypeChoiceElementCount").GetInt32(),
            openChoices.Length);
        Assert.Equal(
            approved.GetProperty("openTypeChoiceAlternativeCount").GetInt32(),
            openChoices.Sum(choice => choice.Element.TypeCodes.Count));
        Assert.Equal(
            approved.GetProperty("optionalChoiceElementCount").GetInt32(),
            choices.Count(choice => choice.Element.Min == 0));
        Assert.Equal(
            approved.GetProperty("requiredChoiceElementCount").GetInt32(),
            choices.Count(choice => choice.Element.Min == 1));
        Assert.All(choices, choice => Assert.Equal("1", choice.Element.Max));
        Assert.All(choices, choice => Assert.True(choice.Element.TypeCodes.Count > 1));
    }

    [Fact]
    public void OpenTypeClassificationUsesTheApprovedCompleteDatatypeSet()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var classification = policy.RootElement.GetProperty("classification");
        var openIds = GetOpenTypeElementIds(policy);
        var extension = Definitions.Value
            .Single(definition => definition.FhirType == "Extension")
            .Elements.Single(element => element.Id == "Extension.value[x]");
        var generatedOpenChoices = GetGeneratedScopeChoices()
            .Where(choice => openIds.Contains(choice.Element.Id))
            .ToArray();

        Assert.Equal(
            "Extension.value[x]-snapshot",
            classification.GetProperty("openTypeAlternativeSetSource").GetString());
        Assert.Equal(10, openIds.Count);
        Assert.Contains("Extension.value[x]", openIds);
        Assert.Equal(54, extension.TypeCodes.Count);
        Assert.Equal(9, generatedOpenChoices.Length);
        Assert.All(
            generatedOpenChoices,
            choice => Assert.Equal(extension.TypeCodes, choice.Element.TypeCodes));
        Assert.Equal(
            [
                "ElementDefinition.defaultValue[x]",
                "ElementDefinition.example.value[x]",
                "ElementDefinition.fixed[x]",
                "ElementDefinition.pattern[x]",
                "Parameters.parameter.value[x]",
                "Task.input.value[x]",
                "Task.output.value[x]",
                "Transport.input.value[x]",
                "Transport.output.value[x]"
            ],
            generatedOpenChoices.Select(choice => choice.Element.Id));
    }

    [Fact]
    public void OrdinaryChoiceNamesAreDeterministicAndCollisionFree()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var openIds = GetOpenTypeElementIds(policy);
        var choices = GetGeneratedScopeChoices()
            .Where(choice => !openIds.Contains(choice.Element.Id))
            .ToArray();
        var original = CreateChoiceMemberSnapshot(choices);
        var reversed = CreateChoiceMemberSnapshot(choices.Reverse());
        var clrIdentities = original.Select(identity =>
        {
            var parts = identity.Split('|');
            var declaringPath = parts[0][..parts[0].LastIndexOf('.')];
            var clrName = parts[1][(parts[1].IndexOf('=') + 1)..];
            return $"{declaringPath}|{clrName}";
        }).ToArray();
        var jsonIdentities = original.Select(identity =>
        {
            var parts = identity.Split('|');
            var declaringPath = parts[0][..parts[0].LastIndexOf('.')];
            return $"{declaringPath}|{parts[2]}";
        }).ToArray();

        Assert.Equal(original, reversed);
        Assert.Equal(
            clrIdentities.Length,
            clrIdentities.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            jsonIdentities.Length,
            jsonIdentities.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(
            "Patient.deceased[x]|boolean=DeceasedBoolean|deceasedBoolean",
            original);
        Assert.Contains(
            "Patient.deceased[x]|dateTime=DeceasedDateTime|deceasedDateTime",
            original);
        Assert.Contains(
            "Claim.item.location[x]|CodeableConcept=LocationCodeableConcept|locationCodeableConcept",
            original);
        Assert.Contains(
            "Claim.item.location[x]|Reference=LocationReference|locationReference",
            original);
    }

    [Fact]
    public void ExtensionBootstrapKeepsSinglePolymorphicPublicPropertyAndMetadataDispatch()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var bootstrap = Assert.Single(
            policy.RootElement
                .GetProperty("openTypeRepresentation")
                .GetProperty("bootstrapOverrides")
                .EnumerateArray());
        var property = typeof(Extension).GetProperty(nameof(Extension.Value));

        Assert.Equal("Extension.value[x]", bootstrap.GetProperty("elementId").GetString());
        Assert.Equal(
            "MyFhirSdk.Core.IFhirExtensionValue",
            bootstrap.GetProperty("clrType").GetString());
        Assert.NotNull(property);
        Assert.Equal(typeof(IFhirExtensionValue), property.PropertyType);
        Assert.Null(typeof(Extension).GetProperty("ValueString"));
        var patient = new Patient
        {
            Extension =
            [
                new Extension { Url = "urn:string", Value = new FhirString("text") },
                new Extension
                {
                    Url = "urn:name",
                    Value = new HumanName { Family = new FhirString("Smith") }
                },
                new Extension
                {
                    Url = "urn:quantity",
                    Value = new SimpleQuantity { Value = new FhirDecimal(1m) }
                }
            ]
        };
        var json = new FhirJsonSerializer().Serialize(patient);

        Assert.Contains("\"valueString\":\"text\"", json, StringComparison.Ordinal);
        Assert.Contains("\"valueHumanName\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"valueQuantity\":{", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedPrimitiveAlternativesRemainExplicitAndFailFast()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        var rules = policy.RootElement.GetProperty("unsupportedAlternativeRules");
        var approved = policy.RootElement
            .GetProperty("approvedR5Inventory")
            .GetProperty("unsupportedPrimitiveAlternativeOccurrences");
        var choices = GetGeneratedScopeChoices();

        Assert.True(rules.GetProperty("preserveOfficialAlternativeInIr").GetBoolean());
        Assert.False(rules.GetProperty("fallbackClrTypesAllowed").GetBoolean());
        Assert.False(rules.GetProperty("omitAlternativeAllowed").GetBoolean());
        Assert.Equal(
            "diagnostic-before-render",
            rules.GetProperty("unresolvedAlternativeDisposition").GetString());
        Assert.Equal(9, CountAlternative(choices, "oid"));
        Assert.Equal(20, CountAlternative(choices, "time"));
        Assert.Equal(9, CountAlternative(choices, "uuid"));
        Assert.Equal(0, CountAlternative(choices, "xhtml"));
        Assert.Equal(9, approved.GetProperty("oid").GetInt32());
        Assert.Equal(20, approved.GetProperty("time").GetInt32());
        Assert.Equal(9, approved.GetProperty("uuid").GetInt32());
        Assert.Equal(0, approved.GetProperty("xhtml").GetInt32());
    }

    private string[] CreateChoiceMemberSnapshot(IEnumerable<OfficialChoice> choices)
    {
        return choices
            .SelectMany(choice => choice.Element.TypeCodes.Select(typeCode =>
            {
                var choiceName = choice.Element.Id[(choice.Element.Id.LastIndexOf('.') + 1)..];
                var fhirStem = choiceName[..^"[x]".Length];
                var stem = _converter.ConvertPropertyName(fhirStem);
                var suffix = _converter.ConvertTypeName(typeCode);
                Assert.True(stem.IsSuccess, $"Invalid choice stem '{fhirStem}'.");
                Assert.True(suffix.IsSuccess, $"Invalid FHIR type code '{typeCode}'.");
                return $"{choice.Element.Id}|{typeCode}={stem.Name}{suffix.Name}|{fhirStem}{suffix.Name}";
            }))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountAlternative(
        IReadOnlyList<OfficialChoice> choices,
        string typeCode) =>
        choices.Sum(choice => choice.Element.TypeCodes.Count(type => type == typeCode));

    private static Type GetPropertyType(Type declaringType, string propertyName)
    {
        return declaringType.GetProperty(propertyName)?.PropertyType
            ?? throw new InvalidOperationException(
                $"Expected public property '{declaringType.FullName}.{propertyName}'.");
    }

    private static IReadOnlySet<string> GetOpenTypeElementIds(JsonDocument policy)
    {
        return policy.RootElement
            .GetProperty("classification")
            .GetProperty("openTypeElementIds")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<OfficialChoice> GetGeneratedScopeChoices()
    {
        using var ownership = ReadPolicy("r5-model-ownership-policy.json");
        var external = ownership.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("fhirType").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        return Definitions.Value
            .Where(definition =>
                definition.Derivation == "specialization" &&
                definition.Kind is "complex-type" or "resource" &&
                !external.Contains(definition.FhirType))
            .SelectMany(definition => definition.Elements
                .Where(element =>
                    element.Id.EndsWith("[x]", StringComparison.Ordinal) &&
                    element.BasePath.StartsWith(definition.FhirType + ".", StringComparison.Ordinal))
                .Select(element => new OfficialChoice(definition, element)))
            .OrderBy(choice => choice.Element.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonDocument ReadPolicy(string fileName)
    {
        return JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Policy", fileName)));
    }

    private static IReadOnlyList<OfficialDefinition> ReadOfficialDefinitions()
    {
        var result = new List<OfficialDefinition>();
        using var archive = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz"));
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null ||
                !entry.Name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(entry.DataStream);
            var root = document.RootElement;
            if (!root.TryGetProperty("resourceType", out var resourceType) ||
                resourceType.GetString() != "StructureDefinition" ||
                !root.TryGetProperty("type", out var type) ||
                !root.TryGetProperty("kind", out var kind) ||
                !root.TryGetProperty("snapshot", out var snapshot) ||
                !snapshot.TryGetProperty("element", out var elements))
            {
                continue;
            }

            result.Add(new OfficialDefinition(
                type.GetString()!,
                kind.GetString()!,
                root.TryGetProperty("derivation", out var derivation)
                    ? derivation.GetString()
                    : null,
                elements.EnumerateArray()
                    .Select(element => new OfficialElement(
                        element.GetProperty("id").GetString()!,
                        element.TryGetProperty("base", out var baseInfo)
                            ? baseInfo.GetProperty("path").GetString()!
                            : element.GetProperty("path").GetString()!,
                        element.TryGetProperty("min", out var min) ? min.GetInt32() : 0,
                        element.TryGetProperty("max", out var max) ? max.GetString()! : "",
                        element.TryGetProperty("type", out var types)
                            ? types.EnumerateArray()
                                .Select(item => item.GetProperty("code").GetString()!)
                                .ToArray()
                            : []))
                    .ToArray()));
        }

        return result;
    }

    private sealed record OfficialDefinition(
        string FhirType,
        string Kind,
        string? Derivation,
        IReadOnlyList<OfficialElement> Elements);

    private sealed record OfficialElement(
        string Id,
        string BasePath,
        int Min,
        string Max,
        IReadOnlyList<string> TypeCodes);

    private sealed record OfficialChoice(
        OfficialDefinition Definition,
        OfficialElement Element);
}
