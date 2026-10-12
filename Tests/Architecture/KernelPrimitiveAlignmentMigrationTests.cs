using System.Text.Json.Nodes;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Tests.Architecture;

public sealed class KernelPrimitiveAlignmentMigrationTests
{
    [Fact]
    public void PrimitiveArrayNullSlotsDoNotShiftLaterValuesOrMetadata()
    {
        const string json = """
            {"resourceType":"Patient","name":[{"given":["A",null,null,"D"],
             "_given":[{"id":"first"},{"id":"metadata-only"},null,{"id":"last"}]}]}
            """;
        var parser = new FhirJsonParser();
        var patient = parser.Parse<Patient>(json);
        var given = Assert.Single(patient.Name).Given;
        Assert.Equal(4, given.Count);
        Assert.Equal("A", given[0].Value);
        Assert.Null(given[1].Value);
        Assert.Equal("metadata-only", given[1].Id);
        Assert.Null(given[2]);
        Assert.Equal("D", given[3].Value);
        Assert.Equal("last", given[3].Id);
        ((IPrimitiveValueAccessor)given[0]).SetUntypedValue(null);
        ((IPrimitiveValueAccessor)given[3]).SetUntypedValue("changed");
        var serialized = new FhirJsonSerializer().Serialize(patient);
        var name = JsonNode.Parse(serialized)!["name"]![0]!;
        Assert.Equal(4, name["given"]!.AsArray().Count);
        Assert.Null(name["given"]![0]);
        Assert.Null(name["given"]![2]);
        Assert.Equal("changed", name["given"]![3]!.GetValue<string>());
        Assert.Equal("first", name["_given"]![0]!["id"]!.GetValue<string>());
        var again = parser.Parse<Patient>(serialized);
        Assert.Equal(4, Assert.Single(again.Name).Given.Count);
        Assert.Null(again.Name[0].Given[2]);
        var issue = Assert.Single(new FhirValidator().Validate(again).Issues);
        Assert.Equal("Patient.name[0].given[2]", issue.Path);
        Assert.Equal(ValidationIssueCode.Cardinality, issue.Code);
    }

    [Theory]
    [InlineData("{\"given\":[\"A\"],\"_given\":[null,{\"id\":\"tail\"}]}" )]
    [InlineData("{\"given\":[\"A\",null],\"_given\":[null,{\"id\":\"tail\"}]}" )]
    public void UnequalRawAndMetadataLengthsKeepMetadataOnlyTail(string nameJson)
    {
        var parser = new FhirJsonParser();
        var patient = parser.Parse<Patient>("{\"resourceType\":\"Patient\",\"name\":[" + nameJson + "]}");
        var given = Assert.Single(patient.Name).Given;
        Assert.Equal(2, given.Count);
        Assert.Equal("A", given[0].Value);
        Assert.Null(given[1].Value);
        Assert.Equal("tail", given[1].Id);
        var roundTrip = parser.Parse<Patient>(new FhirJsonSerializer().Serialize(patient));
        Assert.Equal("tail", roundTrip.Name[0].Given[1].Id);
    }
}
