using System.Text.Json.Nodes;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Rules;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Tests.Architecture;

public sealed class ModelMetadataProviderTests
{
    [Fact]
    public void Parser_UsesInjectedResourceAndDeclaredDatatypeMetadata()
    {
        var provider = CreateProvider(
            declaredDataTypes:
            [
                new DeclaredDataTypeMetadata(
                    typeof(FakeResource),
                    "payload",
                    typeof(FakeDataType))
            ]);
        var parser = new FhirJsonParser(provider);

        var resource = parser.Parse<FakeResource>(
            """
            {
              "resourceType": "FakeResource",
              "payload": { "code": "provider-owned" }
            }
            """);

        var payload = Assert.IsType<FakeDataType>(resource.Payload);
        Assert.Equal("provider-owned", payload.Code);
    }

    [Fact]
    public void Serializer_UsesInjectedExtensionValueMetadata()
    {
        var provider = CreateProvider(
            extensionValues:
            [
                new ExtensionValueMetadata(
                    typeof(FakeDataType),
                    "valueProviderOwned")
            ]);
        var resource = new FakeResource
        {
            Extension =
            [
                new Extension
                {
                    Url = "https://example.test/extension",
                    Value = new FakeDataType { Code = "custom" }
                }
            ]
        };

        var json = new FhirJsonSerializer(provider).Serialize(resource);
        var extension = JsonNode.Parse(json)!["extension"]![0]!;

        Assert.Equal(
            "custom",
            extension["valueProviderOwned"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void ParserAndSerializer_UseInjectedGeneralOpenTypeMetadata()
    {
        var provider = CreateProvider(
            openTypes:
            [
                new OpenTypeValueMetadata(
                    typeof(FakeResource),
                    nameof(FakeResource.Payload),
                    typeof(FakeDataType),
                    "payloadFakeDataType")
            ]);
        var serializer = new FhirJsonSerializer(provider);
        var first = new FakeResource
        {
            Payload = new FakeDataType { Code = "open-type" }
        };

        var json = serializer.Serialize(first);
        var parsed = new FhirJsonParser(provider).Parse<FakeResource>(json);

        Assert.Contains("\"payloadFakeDataType\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"payload\"", json, StringComparison.Ordinal);
        Assert.Equal("open-type", Assert.IsType<FakeDataType>(parsed.Payload).Code);
    }

    [Fact]
    public void Parser_RejectsMultipleValuesForOneOpenTypeProperty()
    {
        var provider = CreateProvider(
            openTypes:
            [
                new OpenTypeValueMetadata(
                    typeof(FakeResource),
                    nameof(FakeResource.Payload),
                    typeof(FakeDataType),
                    "payloadFake"),
                new OpenTypeValueMetadata(
                    typeof(FakeResource),
                    nameof(FakeResource.Payload),
                    typeof(OtherFakeDataType),
                    "payloadOther")
            ]);

        var exception = Assert.Throws<FhirSdkException>(() =>
            new FhirJsonParser(provider).Parse<FakeResource>(
                """
                {
                  "resourceType": "FakeResource",
                  "payloadFake": { "code": "one" },
                  "payloadOther": {}
                }
                """));

        Assert.Contains("more than one value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_UsesInjectedRuleProvider()
    {
        var ruleProvider = new FakeValidationRuleProvider();
        var validator = new FhirValidator(
            ruleProvider,
            new FhirObjectGraphWalker());

        var result = validator.Validate(new FakeResource());

        var issue = Assert.Single(result.Issues);
        Assert.Equal("FakeResource", issue.Path);
        Assert.Equal("fake-provider-rule", issue.RuleId);
    }

    [Fact]
    public void RuleRegistry_ComposesBaseRulesForDerivedModelTypes()
    {
        var baseRule = new FakeValidationRule();
        var derivedRule = new OtherFakeValidationRule();
        var registry = ResourceRuleRegistry.Create(
        [
            new KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>(
                typeof(FakeResource),
                [baseRule]),
            new KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>(
                typeof(DerivedFakeResource),
                [derivedRule])
        ]);

        Assert.Equal([baseRule, derivedRule], registry.GetRules(typeof(DerivedFakeResource)));
    }

    [Fact]
    public void Provider_RejectsDuplicateResourceNames()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ImmutableModelMetadataProvider(
                [
                    new ResourceTypeMetadata(
                        "duplicate",
                        typeof(FakeResource),
                        () => new FakeResource()),
                    new ResourceTypeMetadata(
                        "duplicate",
                        typeof(OtherFakeResource),
                        () => new OtherFakeResource())
                ],
                [typeof(FakeDataType)],
                [],
                []));

        Assert.Contains("Duplicate FHIR resource type name", exception.Message);
    }

    [Fact]
    public void Provider_ReportsUnknownResourceExplicitly()
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<FhirSdkException>(() =>
            provider.GetRequiredResource("MissingResource"));

        Assert.Contains("MissingResource", exception.Message);
    }

    [Fact]
    public void Provider_RejectsConflictingParserExtensionProperties()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(
                extensionValues:
                [
                    new ExtensionValueMetadata(
                        typeof(FakeDataType),
                        "valueConflict"),
                    new ExtensionValueMetadata(
                        typeof(OtherFakeDataType),
                        "valueConflict")
                ]));

        Assert.Contains("Duplicate parser Extension value property", exception.Message);
    }

    [Fact]
    public void ResourceMetadata_WrapsFactoryFailureWithResourceContext()
    {
        var metadata = new ResourceTypeMetadata(
            "FakeResource",
            typeof(FakeResource),
            () => throw new InvalidOperationException("factory failure"));

        var exception = Assert.Throws<FhirSdkException>(metadata.CreateResource);

        Assert.Contains("FakeResource", exception.Message);
        var factoryFailure = Assert.IsType<InvalidOperationException>(
            exception.InnerException);
        Assert.Equal("factory failure", factoryFailure.Message);
    }

    [Fact]
    public void ResourceMetadata_RejectsFactoryReturningWrongResourceType()
    {
        var metadata = new ResourceTypeMetadata(
            "FakeResource",
            typeof(FakeResource),
            () => new OtherFakeResource());

        var exception = Assert.Throws<FhirSdkException>(metadata.CreateResource);

        Assert.Contains(typeof(FakeResource).FullName!, exception.Message);
    }

    [Fact]
    public void Provider_OrdersDatatypeCandidatesDeterministically()
    {
        var provider = new ImmutableModelMetadataProvider(
            [
                new ResourceTypeMetadata(
                    "FakeResource",
                    typeof(FakeResource),
                    () => new FakeResource())
            ],
            [typeof(OtherFakeDataType), typeof(FakeDataType)],
            [],
            []);

        Assert.Equal(
            provider.ConcreteDataTypes.OrderBy(
                type => type.FullName,
                StringComparer.Ordinal),
            provider.ConcreteDataTypes);
    }

    private static ImmutableModelMetadataProvider CreateProvider(
        IReadOnlyList<DeclaredDataTypeMetadata>? declaredDataTypes = null,
        IReadOnlyList<ExtensionValueMetadata>? extensionValues = null,
        IReadOnlyList<OpenTypeValueMetadata>? openTypes = null)
    {
        return new ImmutableModelMetadataProvider(
            [
                new ResourceTypeMetadata(
                    "FakeResource",
                    typeof(FakeResource),
                    () => new FakeResource())
            ],
            [typeof(FakeDataType)],
            declaredDataTypes ?? [],
            extensionValues ?? [],
            openTypes ?? []);
    }

    private class FakeResource : Resource
    {
        public override string ResourceType => "FakeResource";

        public IList<Extension> Extension { get; set; } = [];

        public DataType? Payload { get; set; }
    }

    private sealed class DerivedFakeResource : FakeResource
    {
    }

    private sealed class OtherFakeResource : Resource
    {
        public override string ResourceType => "OtherFakeResource";
    }

    private sealed class FakeDataType : DataType
    {
        public string? Code { get; set; }
    }

    private sealed class OtherFakeDataType : DataType
    {
    }

    private sealed class FakeValidationRuleProvider : IValidationRuleProvider
    {
        private static readonly IReadOnlyList<IFhirValidationRule> Rules =
            [new FakeValidationRule()];

        public IReadOnlyList<IFhirValidationRule> GetRules(Type type) =>
            type == typeof(FakeResource)
                ? Rules
                : Array.Empty<IFhirValidationRule>();
    }

    private sealed class FakeValidationRule : IFhirValidationRule
    {
        public void Validate(
            FhirObjectGraphNode node,
            ICollection<ValidationIssue> issues)
        {
            issues.Add(new ValidationIssue
            {
                Path = node.Path,
                Message = "Injected provider was used.",
                Code = ValidationIssueCode.Profile,
                RuleId = "fake-provider-rule"
            });
        }
    }


    private sealed class OtherFakeValidationRule : IFhirValidationRule
    {
        public void Validate(
            FhirObjectGraphNode node,
            ICollection<ValidationIssue> issues)
        {
        }
    }
}
