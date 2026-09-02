using System.Text.Json;
using MyFhirSdk.CodeGen.Definitions;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Definitions;

public sealed class StructureDefinitionDtoTests
{
    [Fact]
    public void Deserialize_WithSupportedFields_PreservesDefinitionData()
    {
        const string json =
            """
            {
              "resourceType": "StructureDefinition",
              "id": "HumanName",
              "url": "http://hl7.org/fhir/StructureDefinition/HumanName",
              "version": "5.0.0",
              "fhirVersion": "5.0.0",
              "name": "HumanName",
              "type": "HumanName",
              "kind": "complex-type",
              "abstract": false,
              "baseDefinition": "http://hl7.org/fhir/StructureDefinition/DataType",
              "derivation": "specialization",
              "snapshot": {
                "element": [
                  {
                    "id": "HumanName",
                    "path": "HumanName",
                    "min": 0,
                    "max": "*"
                  },
                  {
                    "id": "HumanName.family",
                    "path": "HumanName.family",
                    "min": 0,
                    "max": "1",
                    "base": {
                      "path": "HumanName.family",
                      "min": 0,
                      "max": "1"
                    },
                    "type": [
                      {
                        "code": "string",
                        "profile": [
                          "http://example.org/fhir/StructureDefinition/Profile"
                        ],
                        "targetProfile": [
                          "http://example.org/fhir/StructureDefinition/TargetProfile"
                        ]
                      }
                    ],
                    "short": "Family name",
                    "definition": "The family name.",
                    "contentReference": "#HumanName.family",
                    "sliceName": "official",
                    "label": "Family label",
                    "alias": ["surname"],
                    "representation": ["xmlAttr"],
                    "comment": "Family comment.",
                    "requirements": "Required for matching.",
                    "meaningWhenMissing": "No family is known.",
                    "orderMeaning": "Preferred display order.",
                    "constraint": [
                      {
                        "key": "hn-1",
                        "severity": "error",
                        "human": "Family is valid.",
                        "expression": "family.exists()",
                        "source": "http://example.org/HumanName"
                      }
                    ],
                    "binding": {
                      "strength": "preferred",
                      "description": "Example binding.",
                      "valueSet": "http://example.org/ValueSet/name"
                    },
                    "mustSupport": true,
                    "isModifier": false,
                    "isSummary": true,
                    "condition": ["hn-1"],
                    "fixedString": "Smith",
                    "patternString": "S"
                  }
                ]
              },
              "differential": {
                "element": [
                  {
                    "id": "HumanName",
                    "path": "HumanName"
                  },
                  {
                    "id": "HumanName.family",
                    "path": "HumanName.family"
                  }
                ]
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<StructureDefinitionDto>(json);

        Assert.NotNull(definition);
        Assert.Equal("StructureDefinition", definition.ResourceType);
        Assert.Equal("HumanName", definition.Id);
        Assert.Equal("http://hl7.org/fhir/StructureDefinition/HumanName", definition.Url);
        Assert.Equal("5.0.0", definition.Version);
        Assert.Equal("5.0.0", definition.FhirVersion);
        Assert.Equal("HumanName", definition.Name);
        Assert.Equal("HumanName", definition.Type);
        Assert.Equal("complex-type", definition.Kind);
        Assert.Equal(false, definition.IsAbstract);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/DataType",
            definition.BaseDefinition);
        Assert.Equal("specialization", definition.Derivation);

        Assert.NotNull(definition.Snapshot?.Elements);
        Assert.Equal(2, definition.Snapshot.Elements.Count);

        var family = definition.Snapshot.Elements[1];
        Assert.Equal("HumanName.family", family.Id);
        Assert.Equal("HumanName.family", family.Path);
        Assert.Equal("official", family.SliceName);
        Assert.Equal("Family label", family.Label);
        Assert.Equal(["surname"], family.Aliases);
        Assert.Equal(["xmlAttr"], family.Representations);
        Assert.Equal(0, family.Min);
        Assert.Equal("1", family.Max);
        Assert.Equal("#HumanName.family", family.ContentReference);
        Assert.Equal("Family name", family.Short);
        Assert.Equal("The family name.", family.Definition);
        Assert.Equal("Family comment.", family.Comment);
        Assert.Equal("Required for matching.", family.Requirements);
        Assert.Equal("No family is known.", family.MeaningWhenMissing);
        Assert.Equal("Preferred display order.", family.OrderMeaning);
        Assert.Equal("HumanName.family", family.Base?.Path);
        Assert.Equal(0, family.Base?.Min);
        Assert.Equal("1", family.Base?.Max);
        var constraint = Assert.Single(
            Assert.IsType<List<ElementConstraintDto>>(family.Constraints));
        Assert.Equal("hn-1", constraint.Key);
        Assert.Equal("error", constraint.Severity);
        Assert.Equal("Family is valid.", constraint.Human);
        Assert.Equal("family.exists()", constraint.Expression);
        Assert.Equal("http://example.org/HumanName", constraint.Source);
        Assert.Equal("preferred", family.Binding?.Strength);
        Assert.Equal("Example binding.", family.Binding?.Description);
        Assert.Equal("http://example.org/ValueSet/name", family.Binding?.ValueSet);
        Assert.True(family.MustSupport);
        Assert.False(family.IsModifier);
        Assert.True(family.IsSummary);
        Assert.Equal(["hn-1"], family.Conditions);
        Assert.Equal(
            "Smith",
            family.AdditionalProperties?["fixedString"].GetString());
        Assert.Equal(
            "S",
            family.AdditionalProperties?["patternString"].GetString());

        var familyType = Assert.Single(Assert.IsType<List<ElementTypeDto>>(family.Types));
        Assert.Equal("string", familyType.Code);
        Assert.Equal(
            ["http://example.org/fhir/StructureDefinition/Profile"],
            familyType.Profiles);
        Assert.Equal(
            ["http://example.org/fhir/StructureDefinition/TargetProfile"],
            familyType.TargetProfiles);

        Assert.NotNull(definition.Differential?.Elements);
        Assert.Equal(2, definition.Differential.Elements.Count);
    }

    [Fact]
    public void Deserialize_WithUnknownProperties_IgnoresUnknownData()
    {
        const string json =
            """
            {
              "resourceType": "StructureDefinition",
              "unknownRoot": true,
              "snapshot": {
                "unknownSnapshot": "ignored",
                "element": [
                  {
                    "id": "Period.start",
                    "unknownElement": 42,
                    "slicing": {
                      "ordered": false
                    },
                    "type": [
                      {
                        "code": "dateTime",
                        "unknownType": {}
                      }
                    ]
                  }
                ]
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<StructureDefinitionDto>(json);

        Assert.NotNull(definition);
        Assert.Equal("StructureDefinition", definition.ResourceType);
        var element = Assert.Single(
            Assert.IsType<List<ElementDefinitionDto>>(definition.Snapshot?.Elements));
        Assert.Equal(
            JsonValueKind.Object,
            element.Slicing?.ValueKind);
        Assert.Equal(
            42,
            element.AdditionalProperties?["unknownElement"].GetInt32());
        var elementType = Assert.Single(
            Assert.IsType<List<ElementTypeDto>>(element.Types));
        Assert.Equal("dateTime", elementType.Code);
    }

    [Fact]
    public void Deserialize_WithMissingFields_LeavesFieldsNullForLaterValidation()
    {
        var definition = JsonSerializer.Deserialize<StructureDefinitionDto>("{}");

        Assert.NotNull(definition);
        Assert.Null(definition.ResourceType);
        Assert.Null(definition.IsAbstract);
        Assert.Null(definition.Snapshot);
        Assert.Null(definition.Differential);
    }
}
