using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;
using P = MyFhirSdk.R4.Primitives;
using R = MyFhirSdk.R4.Resources;
using T = MyFhirSdk.R4.Types;

namespace MyFhirSdk.Tests.FhirVersions.V5;

public sealed class R4IntegrationTests
{
    internal static FhirModelContext Context() => new(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);

    [Fact]
    public void CompleteProviderOwnsEveryDeclarationAndConcreteResourceFactory()
    {
        var assembly = typeof(R.Patient).Assembly;
        Assert.Equal("MyFhirSdk.R4.Models", assembly.GetName().Name);
        Assert.All(assembly.GetReferencedAssemblies(), a => Assert.True(a.Name!.StartsWith("System.") ||
            a.Name is "MyFhirSdk.Runtime" or "MyFhirSdk.ModelContracts" or "MyFhirSdk.ModelSupport", a.FullName));
        Assert.Empty(assembly.GetForwardedTypes());
        Assert.DoesNotContain(assembly.GetCustomAttributesData(), a => a.AttributeType.Name == "InternalsVisibleToAttribute");
        var declarations = assembly.GetExportedTypes().Where(t => typeof(FhirObject).IsAssignableFrom(t)).ToHashSet();
        Assert.Equal(675, declarations.Count);
        Assert.True(declarations.SetEquals(R4ModelProvider.Metadata.ModelTypes));
        Assert.Equal(146, R4ModelProvider.Metadata.Resources.Count);
        Assert.Equal(19, R4ModelProvider.Primitives.Definitions.Count);
        Assert.Same(assembly, typeof(T.SimpleQuantity).Assembly);
        Assert.Same(assembly, typeof(P.FhirString).Assembly);
        var context = Context();
        foreach (var entry in R4ModelProvider.Metadata.Resources)
        {
            Assert.Equal(entry.ResourceType, context.CreateResource(entry.FhirTypeName).GetType());
            var json = new FhirJsonSerializer(context).Serialize(entry.Factory());
            Assert.Equal(entry.ResourceType, new FhirJsonParser(context).Parse<Resource>(json).GetType());
        }
        Assert.True(context.Capabilities.CompleteResourceSet);
        Assert.True(context.Capabilities.PrimitiveValidation);
        Assert.True(context.Capabilities.RequiredAndChoiceValidation);
        Assert.False(context.Capabilities.InvariantValidation);
        Assert.False(context.Capabilities.ProfileValidation);
    }

    [Fact]
    public void BootstrapBackboneContainedAndPrimitiveArrayMetadataRoundTrip()
    {
        const string json = """
            {"resourceType":"Patient","meta":{"tag":[{"code":"tag"}],"security":[{"code":"sec"}]},
             "text":{"status":"generated","div":"<div xmlns='http://www.w3.org/1999/xhtml'>text</div>"},
             "extension":[{"url":"https://example.org/q","valueQuantity":{"value":1.2300}}],
             "name":[{"given":["Ada",null],"_given":[null,{"extension":[{"url":"https://example.org/missing","valueString":"known"}]}]}],
             "contact":[{"name":{"family":"Contact"}}],"contained":[{"resourceType":"Organization","id":"nested"}]}
            """;
        var context = Context(); var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
        var value = parser.Parse<R.Patient>(json);
        Assert.IsType<T.Coding>(value.Meta!.Tag.Single());
        Assert.IsType<T.Coding>(value.Meta.Security.Single());
        Assert.IsType<R.Organization>(value.Contained.Single());
        Assert.IsType<T.Quantity>(value.Extension.Single().Value);
        Assert.Equal("Contact", value.Contact.Single().Name!.Family!.Value);
        Assert.Equal(2, value.Name.Single().Given.Count);
        Assert.Equal("known", Assert.IsType<P.FhirString>(value.Name.Single().Given[1].Extension.Single().Value).Value);
        var output = serializer.Serialize(value);
        Assert.Contains("1.2300", output);
        using var document = JsonDocument.Parse(output);
        var name = document.RootElement.GetProperty("name")[0];
        Assert.Equal(JsonValueKind.Null, name.GetProperty("given")[1].ValueKind);
        Assert.Equal(2, name.GetProperty("_given").GetArrayLength());
        Assert.Equal(output, serializer.Serialize(parser.Parse<R.Patient>(output)));
        Assert.True(new FhirValidator(context).Validate(value).IsValid);
    }

    [Theory]
    [InlineData("Boolean", "true")]
    [InlineData("Integer", "-12")]
    [InlineData("UnsignedInt", "0")]
    [InlineData("PositiveInt", "1")]
    [InlineData("Decimal", "1.2300")]
    [InlineData("String", "\"hello\"")]
    [InlineData("Markdown", "\"**hello**\"")]
    [InlineData("Code", "\"final\"")]
    [InlineData("Id", "\"abc-123\"")]
    [InlineData("Uri", "\"urn:example:one\"")]
    [InlineData("Url", "\"https://example.org/a\"")]
    [InlineData("Canonical", "\"https://example.org/StructureDefinition/a|1.0\"")]
    [InlineData("Oid", "\"urn:oid:1.2.3\"")]
    [InlineData("Uuid", "\"urn:uuid:550e8400-e29b-41d4-a716-446655440000\"")]
    [InlineData("Base64Binary", "\"aGVsbG8=\"")]
    [InlineData("Date", "\"2024-02-29\"")]
    [InlineData("DateTime", "\"2024-02-29T10:20:30+08:00\"")]
    [InlineData("Instant", "\"2024-02-29T10:20:30Z\"")]
    [InlineData("Time", "\"10:20:30\"")]
    public void EveryR4PrimitiveRunsThroughOpenTypeCodecAndValidator(string suffix, string token)
    {
        var context = Context();
        var json = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"v\",\"value" + suffix + "\":" + token + "}]}";
        var value = new FhirJsonParser(context).Parse<R.Parameters>(json);
        Assert.Equal("MyFhirSdk.R4.Primitives.Fhir" + suffix, value.Parameter.Single().Value!.GetType().FullName);
        Assert.True(new FhirValidator(context).Validate(value).IsValid);
        var output = new FhirJsonSerializer(context).Serialize(value);
        using var document = JsonDocument.Parse(output);
        var raw = document.RootElement.GetProperty("parameter")[0].GetProperty("value" + suffix);
        using var expected = JsonDocument.Parse(token);
        Assert.Equal(expected.RootElement.ValueKind, raw.ValueKind);
        if (raw.ValueKind == JsonValueKind.String) Assert.Equal(expected.RootElement.GetString(), raw.GetString());
        else Assert.Equal(token, raw.GetRawText());
    }

    [Theory]
    [InlineData("Date", "\"2023-02-29\"")]
    [InlineData("DateTime", "\"2024-02-30\"")]
    [InlineData("Instant", "\"2024-01-01\"")]
    [InlineData("Time", "\"25:00:00\"")]
    [InlineData("Code", "\" leading\"")]
    [InlineData("Id", "\"bad id\"")]
    [InlineData("Base64Binary", "\"***\"")]
    [InlineData("PositiveInt", "0")]
    [InlineData("UnsignedInt", "-1")]
    [InlineData("Oid", "\"1.2.3\"")]
    [InlineData("Uuid", "\"invalid\"")]
    public void InvalidPrimitiveLexicalValuesProduceFormatIssues(string suffix, string token)
    {
        var context = Context();
        var json = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"v\",\"value" + suffix + "\":" + token + "}]}";
        var value = new FhirJsonParser(context).Parse<R.Parameters>(json);
        Assert.Contains(new FhirValidator(context).Validate(value).Issues, i => i.Code == ValidationIssueCode.PrimitiveFormat);
    }

    [Theory]
    [InlineData("Boolean", "\"true\"")]
    [InlineData("Integer", "\"1\"")]
    [InlineData("UnsignedInt", "\"1\"")]
    [InlineData("Decimal", "\"1.2\"")]
    [InlineData("String", "1")]
    public void PrimitiveJsonTokenKindsAreEnforced(string suffix, string token)
    {
        var json = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"v\",\"value" + suffix + "\":" + token + "}]}";
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(Context()).Parse<R.Parameters>(json));
    }

    [Fact]
    public void RequiredChoiceAndCollectionRulesUseFreshGeneratedComposition()
    {
        var validator = new FhirValidator(Context());
        var observation = new R.Observation { ValueString = new("x"), ValueInteger = new(1) };
        var issues = validator.Validate(observation).Issues;
        Assert.Contains(issues, i => i.Path == "Observation.status" && i.Code == ValidationIssueCode.Required);
        Assert.Contains(issues, i => i.Path == "Observation.code" && i.Code == ValidationIssueCode.Required);
        Assert.Contains(issues, i => i.Path == "Observation.value[x]" && i.Code == ValidationIssueCode.ChoiceElement);
        Assert.All(issues.Where(i => i.Code is ValidationIssueCode.Required or ValidationIssueCode.ChoiceElement), i => Assert.NotNull(i.RuleId));
        var outcome = new R.OperationOutcome();
        Assert.Contains(validator.Validate(outcome).Issues, i => i.Code == ValidationIssueCode.Cardinality && i.Path == "OperationOutcome.issue");
        var patient = new R.Patient { Name = null! };
        Assert.Contains(validator.Validate(patient).Issues, i => i.Code == ValidationIssueCode.Cardinality && i.Path == "Patient.name");
        patient.Name = [null!];
        Assert.Contains(validator.Validate(patient).Issues, i => i.Code == ValidationIssueCode.Cardinality && i.Path == "Patient.name[0]");
        var medication = new R.MedicationRequest();
        Assert.Contains(validator.Validate(medication).Issues, i => i.Code == ValidationIssueCode.ChoiceElement && i.Path == "MedicationRequest.medication[x]");
    }

    [Fact]
    public void OpenComplexTypesAndNestedResourcesUseTheR4Factories()
    {
        const string json = """{"resourceType":"Parameters","parameter":[{"name":"q","valueQuantity":{"value":2}},{"name":"r","resource":{"resourceType":"Organization","id":"o"}},{"name":"parts","part":[{"name":"s","valueString":"yes"}]}]}""";
        var context = Context(); var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
        var parameters = parser.Parse<R.Parameters>(json);
        Assert.IsType<T.Quantity>(parameters.Parameter[0].Value);
        Assert.IsType<R.Organization>(parameters.Parameter[1].Resource);
        Assert.IsType<P.FhirString>(parameters.Parameter[2].Part.Single().Value);
        Assert.Equal(serializer.Serialize(parameters), serializer.Serialize(parser.Parse<R.Parameters>(serializer.Serialize(parameters))));
        Assert.True(new FhirValidator(context).Validate(parameters).IsValid);
    }

    [Theory]
    [InlineData("\"given\":[\"Ada\"],\"_given\":[null,{\"id\":\"extra\"}]", 2)]
    [InlineData("\"given\":[\"Ada\",\"Bob\"],\"_given\":[{\"id\":\"first\"}]", 2)]
    [InlineData("\"_given\":[{\"extension\":[{\"url\":\"https://example.org/missing\",\"valueString\":\"known\"}]}]", 1)]
    public void UnequalAndMetadataOnlyPrimitiveArraysKeepTheirPositions(string fields, int count)
    {
        var context = Context(); var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
        var patient = parser.Parse<R.Patient>("{\"resourceType\":\"Patient\",\"name\":[{" + fields + "}]}");
        Assert.Equal(count, patient.Name.Single().Given.Count);
        var roundTrip = parser.Parse<R.Patient>(serializer.Serialize(patient));
        Assert.Equal(patient.Name.Single().Given.Select(p => (p.Value, p.Id, p.Extension.Count)),
            roundTrip.Name.Single().Given.Select(p => (p.Value, p.Id, p.Extension.Count)));
    }

    [Theory]
    [InlineData("FutureResource")]
    [InlineData("Resource")]
    [InlineData("DomainResource")]
    [InlineData("ActorDefinition")]
    public void UnknownAbstractAndR5OnlyResourcesAreRejected(string name)
    {
        var context = Context();
        Assert.Throws<FhirSdkException>(() => context.CreateResource(name));
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(context).Parse<Resource>("{\"resourceType\":\"" + name + "\"}"));
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(context).Parse<R.Patient>("{\"resourceType\":\"Patient\",\"contained\":[{\"resourceType\":\"" + name + "\"}]}"));
    }

    [Fact]
    public void CrossVersionRootsContainedExtensionsAndOpenValuesFailEarly()
    {
        var context = Context(); var serializer = new FhirJsonSerializer(context); var validator = new FhirValidator(context);
        void Rejected(Resource value)
        {
            Assert.StartsWith("FHIR_MODEL_OWNERSHIP:", Assert.Throws<FhirSdkException>(() => serializer.Serialize(value)).Message);
            Assert.StartsWith("FHIR_MODEL_OWNERSHIP:", Assert.Throws<FhirSdkException>(() => validator.Validate(value)).Message);
        }
        Rejected(new MyFhirSdk.Resources.Patient());
        Rejected(new R.Patient { Contained = [new MyFhirSdk.Resources.Patient()] });
        Rejected(new R.Patient { Extension = [new Extension { Url = "https://example.org/a", Value = new MyFhirSdk.Types.Quantity() }] });
        Rejected(new R.Parameters { Parameter = [new R.ParametersParameter { Name = new("v"), Value = new MyFhirSdk.Primitives.FhirString("foreign") }] });
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(context).Parse<MyFhirSdk.Resources.Patient>("{\"resourceType\":\"Patient\"}"));
        Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer(TestContexts.R5).Serialize(new R.Patient()));
        Assert.Throws<FhirSdkException>(() => new FhirValidator(TestContexts.R5).Validate(new R.Patient()));
    }

    [Fact]
    public async Task ParallelR4R5ConsumersKeepFactoriesAndPrimitiveRulesIsolated()
    {
        var r4 = Context(); var r5 = TestContexts.R5;
        var p4 = new FhirJsonParser(r4); var p5 = new FhirJsonParser(r5);
        var s4 = new FhirJsonSerializer(r4); var s5 = new FhirJsonSerializer(r5);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            var a = p4.Parse<Resource>("""{"resourceType":"Patient","photo":[{"size":42}]}""");
            var b = p5.Parse<Resource>("""{"resourceType":"Patient","photo":[{"size":"42"}]}""");
            Assert.IsType<R.Patient>(a); Assert.IsType<MyFhirSdk.Resources.Patient>(b);
            Assert.Contains("\"size\":42", s4.Serialize(a)); Assert.Contains("\"size\":\"42\"", s5.Serialize(b));
            Assert.True(new FhirValidator(r4).Validate(a).IsValid);
            Assert.True(new FhirValidator(r5).Validate(b).IsValid);
        })));
        Assert.IsType<MyFhirSdk.Resources.Patient>(new FhirJsonParser(TestContexts.R5).Parse<Resource>("{\"resourceType\":\"Patient\"}"));
    }

    [Fact]
    public void R4ApiMatchesApprovedSnapshot()
    {
        var actual = MyFhirSdk.Tests.Architecture.R5ModelPublicApiSnapshot.Create(typeof(R.Patient).Assembly.GetExportedTypes());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ActualR4ModelApi.txt"), actual + "\n");
        var approved = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ApprovedR4ModelApi.txt")).Replace("\r\n", "\n").TrimEnd();
        Assert.Equal(approved, actual);
    }

    [Theory]
    [InlineData("Base64Binary", "\"aGVs bG8=\"", false)]
    [InlineData("Time", "\"10:20:30.1234567890\"", false)]
    [InlineData("DateTime", "\"2024-02-29T10:20:30.1234567890Z\"", false)]
    [InlineData("Instant", "\"2024-02-29T10:20:30.1234567890+08:00\"", false)]
    [InlineData("Decimal", "1234567890123456789", false)]
    [InlineData("Decimal", "0.123456789012345678", false)]
    [InlineData("Code", "\"alpha\\tbeta\"", false)]
    [InlineData("Base64Binary", "\"aGVsbG8=\"", true)]
    [InlineData("Time", "\"10:20:30.123456789\"", true)]
    [InlineData("DateTime", "\"2024-02-29T10:20:30.123456789Z\"", true)]
    [InlineData("Instant", "\"2024-02-29T10:20:30.123456789Z\"", true)]
    [InlineData("Decimal", "123456789012345678", true)]
    [InlineData("Code", "\"alpha beta\"", true)]
    public void R4LexicalDifferencesPreserveTheWireLiteralAndR5Behavior(string suffix, string token, bool r5Valid)
    {
        var json = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"v\",\"value" + suffix + "\":" + token + "}]}";
        var r4 = Context();
        var value = new FhirJsonParser(r4).Parse<R.Parameters>(json);
        Assert.True(new FhirValidator(r4).Validate(value).IsValid);
        var output = new FhirJsonSerializer(r4).Serialize(value);
        using var expected = JsonDocument.Parse(token);
        using var actual = JsonDocument.Parse(output);
        var raw = actual.RootElement.GetProperty("parameter")[0].GetProperty("value" + suffix);
        if (raw.ValueKind == JsonValueKind.String) Assert.Equal(expected.RootElement.GetString(), raw.GetString());
        else Assert.Equal(token, raw.GetRawText());
        var r5 = new FhirJsonParser(TestContexts.R5).Parse<MyFhirSdk.Resources.Parameters>(json);
        Assert.Equal(r5Valid, new FhirValidator(TestContexts.R5).Validate(r5).IsValid);
    }

    [Theory]
    [InlineData("Base64Binary", "\"====\"")]
    [InlineData("Base64Binary", "\"aGVsbG8\"")]
    [InlineData("Base64Binary", "\"aGVs *bG8=\"")]
    [InlineData("Base64Binary", "\"\"")]
    [InlineData("Time", "\"25:20:30.1234567890\"")]
    [InlineData("Time", "\"10:20:30.1234567890x\"")]
    [InlineData("DateTime", "\"2024-02-30T10:20:30.1234567890Z\"")]
    [InlineData("DateTime", "\"2024-02-29T10:20:30.1234567890\"")]
    [InlineData("DateTime", "\"0000-02-29T10:20:30.1234567890Z\"")]
    [InlineData("Instant", "\"2024-02-29T10:20:30.1234567890+14:01\"")]
    [InlineData("Code", "\"alpha  beta\"")]
    [InlineData("Code", "\"alpha beta\\n\"")]
    public void R4LexicalAdaptersStillRejectMalformedValues(string suffix, string token)
    {
        var json = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"v\",\"value" + suffix + "\":" + token + "}]}";
        var context = Context();
        var value = new FhirJsonParser(context).Parse<R.Parameters>(json);
        Assert.Contains(new FhirValidator(context).Validate(value).Issues, i => i.Code == ValidationIssueCode.PrimitiveFormat);
    }

    [Theory]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("1.")]
    [InlineData("1e")]
    [InlineData("NaN")]
    [InlineData("1e1x")]
    public void R4DecimalLiteralValidationDoesNotAcceptMalformedNumbers(string literal)
    {
        var parameters = new R.Parameters { Parameter = [new R.ParametersParameter { Name = new("v"), Value = new P.FhirDecimal(literal) }] };
        Assert.Contains(new FhirValidator(Context()).Validate(parameters).Issues, i => i.Code == ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ProviderInventoryAndValidationCapabilitiesAreExplicitAndExecutable()
    {
        var metadata = R4ModelProvider.Metadata;
        var rules = R4ModelProvider.Validation.Rules;
        Assert.Equal(rules.Count, rules.Select(r => (r.AppliesTo, r.RuleId)).Distinct().Count());
        Assert.All(rules, rule =>
        {
            Assert.False(rule.AppliesTo.IsAbstract);
            var value = Assert.IsAssignableFrom<FhirObject>(Activator.CreateInstance(rule.AppliesTo));
            // Every registered rule executes on its actual compiled model declaration.
            Assert.NotNull(rule.Evaluate(new(value, rule.AppliesTo.Name)));
        });
        Assert.DoesNotContain(R4ModelProvider.Primitives.Definitions, p => p.FhirTypeName is "integer64" or "xhtml");
        var inventory = new
        {
            identity = R4ModelProvider.Identity,
            capabilities = R4ModelProvider.Validation.Capabilities,
            modelTypes = metadata.ModelTypes.Select(t => t.FullName).Order(StringComparer.Ordinal),
            resources = metadata.Resources.Select(r => new { fhirType = r.FhirTypeName, clrType = r.ResourceType.FullName }),
            primitives = R4ModelProvider.Primitives.Definitions.Select(p => new { fhirType = p.FhirTypeName, clrType = p.PrimitiveType.FullName }),
            rules = rules.Select(r => new { clrType = r.AppliesTo.FullName, ruleId = r.RuleId }).OrderBy(r => r.clrType, StringComparer.Ordinal).ThenBy(r => r.ruleId, StringComparer.Ordinal)
        };
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ActualR4ProviderInventory.json"),
            JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
