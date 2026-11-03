using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Types;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Tests.Architecture;

public sealed class KernelBehaviorMigrationTests
{
    [Theory]
    [MemberData(nameof(PrimitiveRuntimeContractTests.CodecCases), MemberType = typeof(PrimitiveRuntimeContractTests))]
    public void EveryPrimitiveCrossesKernelBoundaryWithValueAndMetadata(Type type, string rawJson)
    {
        var definition = TestContexts.R5.Primitives.GetRequired(type);
        using var raw = JsonDocument.Parse(rawJson);
        var value = definition.Codec.CreatePrimitive(type, raw.RootElement);
        var element = Assert.IsAssignableFrom<Element>(value);
        element.Id = "primitive-id";
        element.Extension.Add(new Extension { Url = "https://example.test/source", Value = new FhirString("origin") });
        var accessor = Assert.IsAssignableFrom<IPrimitiveValueAccessor>(value);
        Assert.Equal(typeof(Resource).Assembly, typeof(IPrimitiveValueAccessor).Assembly);
        Assert.NotEqual(typeof(Resource).Assembly, type.Assembly);
        Assert.Equal(definition.ValueType, accessor.ValueType);
        var original = accessor.UntypedValue;
        Assert.NotNull(original);
        Assert.True(definition.Validator.IsValid(value));
        accessor.SetUntypedValue(null);
        Assert.Null(accessor.UntypedValue);
        var literalBacked = value is FhirDecimal or FhirInteger64;
        Assert.Equal(literalBacked, definition.Codec.HasRawValue(value));
        accessor.SetUntypedValue(original);
        Assert.True(definition.Codec.HasRawValue(value));

        var patient = new Patient();
        patient.Extension.Add(new Extension { Url = "https://example.test/value", Value = (IFhirExtensionValue)value });
        var serializer = new FhirJsonSerializer(TestContexts.R5);
        var parser = new FhirJsonParser(TestContexts.R5);
        var json = serializer.Serialize(patient);
        var property = TestContexts.R5.Metadata.GetRequiredExtensionValuePropertyName(type);
        var extension = JsonNode.Parse(json)!["extension"]![0]!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rawJson), extension[property]));
        Assert.Equal("primitive-id", extension["_" + property]!["id"]!.GetValue<string>());
        var roundTrip = parser.Parse<Patient>(json);
        var parsed = Assert.IsAssignableFrom<Element>(Assert.Single(roundTrip.Extension).Value);
        Assert.Equal(type, parsed.GetType());
        Assert.Equal(original, ((IPrimitiveValueAccessor)parsed).UntypedValue);
        Assert.Equal("origin", Assert.IsType<FhirString>(Assert.Single(parsed.Extension).Value).Value);
        Assert.True(new FhirValidator(TestContexts.R5).Validate(roundTrip).IsValid);

        ((IPrimitiveValueAccessor)parsed).SetUntypedValue(null);
        if (literalBacked)
        {
            // Clearing the accessor retains the original read-only Literal contract.
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rawJson),
                JsonNode.Parse(serializer.Serialize(roundTrip))!["extension"]![0]![property]));
            var empty = (Element)Activator.CreateInstance(type)!;
            empty.Id = parsed.Id;
            empty.Extension = parsed.Extension;
            roundTrip.Extension[0].Value = (IFhirExtensionValue)empty;
        }
        var metadataOnlyJson = serializer.Serialize(roundTrip);
        var metadataOnly = JsonNode.Parse(metadataOnlyJson)!["extension"]![0]!;
        Assert.False(metadataOnly.AsObject().ContainsKey(property));
        Assert.Equal("primitive-id", metadataOnly["_" + property]!["id"]!.GetValue<string>());
        var restored = Assert.IsAssignableFrom<Element>(Assert.Single(parser.Parse<Patient>(metadataOnlyJson).Extension).Value);
        Assert.Equal(type, restored.GetType());
        Assert.Null(((IPrimitiveValueAccessor)restored).UntypedValue);
        Assert.Equal("primitive-id", restored.Id);
    }

    [Fact]
    public void DefaultFactoryRoundTripsEveryConcreteR5ResourceAcrossKernelBase()
    {
        // Reflection is a test oracle only; the default engines use generated registration.
        var types = typeof(Patient).Assembly.GetExportedTypes().Where(type =>
            typeof(Resource).IsAssignableFrom(type) && !type.IsAbstract).OrderBy(type => type.Name).ToArray();
        Assert.Equal(158, types.Length);
        var parser = new FhirJsonParser(TestContexts.R5);
        var serializer = new FhirJsonSerializer(TestContexts.R5);
        foreach (var type in types)
        {
            var json = "{\"resourceType\":\"" + type.Name + "\",\"id\":\"kernel-factory\"}";
            var resource = parser.Parse<Resource>(json);
            Assert.Equal(type, resource.GetType());
            Assert.Equal("MyFhirSdk.Runtime", typeof(Resource).Assembly.GetName().Name);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serializer.Serialize(resource))), type.Name);
        }
    }

    [Fact]
    public void RuntimeBootstrapFieldsAndSdkDatatypesRoundTripTogether()
    {
        const string json = """
            {"resourceType":"Patient","id":"kernel-patient",
             "meta":{"versionId":"7","lastUpdated":"2026-10-12T01:00:00.0000000+00:00","source":"https://example.test/source",
                     "profile":["https://example.test/profile"],"tag":[{"system":"https://example.test/tag","code":"a"}],
                     "security":[{"system":"https://example.test/security","code":"b"}]},
             "text":{"status":"generated","div":"<div xmlns=\"http://www.w3.org/1999/xhtml\">Patient</div>"},
             "contained":[{"resourceType":"Patient","id":"inside","active":false}],
             "extension":[{"url":"https://example.test/nested","extension":[{"url":"part","valueString":"nested"}]}],
             "modifierExtension":[{"url":"https://example.test/modifier","valueBoolean":false}]}
            """;
        var parser = new FhirJsonParser(TestContexts.R5);
        var patient = parser.Parse<Patient>(json);
        Assert.IsType<Meta>(patient.Meta);
        Assert.IsType<Narrative>(patient.Text);
        Assert.IsType<Coding>(Assert.Single(patient.Meta!.Tag));
        Assert.IsType<Coding>(Assert.Single(patient.Meta.Security));
        Assert.False(Assert.IsType<Patient>(Assert.Single(patient.Contained)).Active!.Value);
        Assert.Equal("nested", Assert.IsType<FhirString>(Assert.Single(Assert.Single(patient.Extension).Extension).Value).Value);
        Assert.True(new FhirValidator(TestContexts.R5).Validate(patient).IsValid);
        var serialized = new FhirJsonSerializer(TestContexts.R5).Serialize(patient);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serialized)), serialized);
    }

    [Theory]
    [InlineData("valueString", "\"sample\"", typeof(FhirString))]
    [InlineData("valueBoolean", "false", typeof(FhirBoolean))]
    [InlineData("valueQuantity", "{\"value\":1.5,\"unit\":\"mg\"}", typeof(Quantity))]
    public void DefaultOpenTypeCompositionPreservesSdkValueThroughRuntimeGraph(string property, string value, Type type)
    {
        var json = "{\"resourceType\":\"Task\",\"status\":\"requested\",\"intent\":\"order\",\"input\":[{\"type\":{\"text\":\"test\"},\"" + property + "\":" + value + "}]}";
        var resource = new FhirJsonParser(TestContexts.R5).Parse<MyFhirSdk.Resources.Task>(json);
        Assert.Equal(type, Assert.Single(resource.Input).Value!.GetType());
        Assert.True(new FhirValidator(TestContexts.R5).Validate(resource).IsValid);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(new FhirJsonSerializer(TestContexts.R5).Serialize(resource))));
    }

    [Fact]
    public void DefaultCompositionDoesNotDiscoverExternalTypes()
    {
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(TestContexts.R5).Parse<Resource>("{\"resourceType\":\"ExternalResource\"}"));
        Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer(TestContexts.R5).Serialize(new ExternalResource()));
        Assert.Throws<FhirSdkException>(() => new FhirValidator(TestContexts.R5).Validate(new ExternalResource()));
        var patient = new Patient();
        patient.Extension.Add(new Extension { Url = "https://example.test/value", Value = new ExternalDatatype { Id = "external" } });
        Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer(TestContexts.R5).Serialize(patient));
    }

    private sealed class ExternalResource : Resource { public override string ResourceType => "ExternalResource"; }
    private sealed class ExternalDatatype : DataType;
}
