using System.Text.Json.Nodes;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PhaseBPrimitiveHandoffTests
{
    [Fact]
    public void ThinGeneratedStyleWrapperCanSerializeParseAndValidate()
    {
        var modelMetadata = CreateModelMetadata();
        var primitiveDefinitions = CreatePrimitiveDefinitions();
        var serializer = new FhirJsonSerializer(
            modelMetadata,
            primitiveDefinitions);
        var parser = new FhirJsonParser(
            modelMetadata,
            primitiveDefinitions);
        var validator = new FhirValidator(
            ResourceRuleRegistry.Create([]),
            primitiveDefinitions,
            new FhirObjectGraphWalker());
        var resource = new PhaseBGeneratedResource
        {
            Alias = new PhaseBGeneratedString("generated-value")
        };

        var json = serializer.Serialize(resource);
        var parsed = parser.Parse<PhaseBGeneratedResource>(json);
        var validation = validator.Validate(parsed);

        Assert.Equal(
            "generated-value",
            JsonNode.Parse(json)!["alias"]!.GetValue<string>());
        Assert.Equal("generated-value", parsed.Alias?.Value);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ThinGeneratedStyleWrapperUsesRuntimeFormatValidator()
    {
        var primitiveDefinitions = CreatePrimitiveDefinitions();
        var validator = new FhirValidator(
            ResourceRuleRegistry.Create([]),
            primitiveDefinitions,
            new FhirObjectGraphWalker());
        var resource = new PhaseBGeneratedResource
        {
            Alias = new PhaseBGeneratedString(string.Empty)
        };

        var result = validator.Validate(resource);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("PhaseBGeneratedResource.alias", issue.Path);
        Assert.Equal(ValidationIssueCode.PrimitiveFormat, issue.Code);
    }

    private static ImmutableModelMetadataProvider CreateModelMetadata()
    {
        return new ImmutableModelMetadataProvider(
            [
                new ResourceTypeMetadata(
                    "PhaseBGeneratedResource",
                    typeof(PhaseBGeneratedResource),
                    () => new PhaseBGeneratedResource())
            ],
            [],
            [],
            []);
    }

    private static PrimitiveRegistry CreatePrimitiveDefinitions()
    {
        var generatedDefinition = new PrimitiveDefinition(
            "phaseBString",
            typeof(PhaseBGeneratedString),
            typeof(string),
            PrimitiveCodecs.String,
            PrimitiveValidators.String);

        return PrimitiveRegistry.Create(
            PrimitiveRegistry.Default.Definitions.Append(generatedDefinition));
    }

    public sealed class PhaseBGeneratedString : PrimitiveType<string>
    {
        public PhaseBGeneratedString()
        {
        }

        public PhaseBGeneratedString(string? value)
            : base(value)
        {
        }
    }

    public sealed class PhaseBGeneratedResource : Resource
    {
        public override string ResourceType => "PhaseBGeneratedResource";

        public PhaseBGeneratedString? Alias { get; set; }
    }
}
