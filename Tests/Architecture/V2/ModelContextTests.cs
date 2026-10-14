using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Tests.FhirVersions.V2;
using MyFhirSdk.Validation;
using R4P = MyFhirSdk.R4.Primitives;
using R4R = MyFhirSdk.R4.Resources;
using R4T = MyFhirSdk.R4.Types;
using R5P = MyFhirSdk.Primitives;
using R5R = MyFhirSdk.Resources;
using R5T = MyFhirSdk.Types;

namespace MyFhirSdk.Tests.Architecture.V2;

public sealed class ModelContextTests
{
    internal static FhirModelContext R4() => new(R4ProviderFixture.CreateMetadata(),
        R4ProviderFixture.CreatePrimitives(), R4ProviderFixture.CreateRules());

    [Fact]
    public void PublicContractsAndIndependentProviderHaveNoSdkOrEngineDependency()
    {
        var contracts = typeof(ModelIdentity).Assembly;
        var provider = typeof(R4ProviderFixture).Assembly;
        Assert.Equal("MyFhirSdk.ModelContracts", contracts.GetName().Name);
        Assert.Equal("MyFhirSdk.R4.V2.Provider", provider.GetName().Name);
        Assert.All(contracts.GetReferencedAssemblies(), a => Assert.True(a.Name!.StartsWith("System.") || a.Name == "MyFhirSdk.Runtime"));
        Assert.All(provider.GetReferencedAssemblies(), a => Assert.True(a.Name!.StartsWith("System.") ||
            a.Name is "MyFhirSdk.Runtime" or "MyFhirSdk.ModelContracts" or "MyFhirSdk.R4.V1.Models"));
        foreach (var type in contracts.GetExportedTypes())
        {
            Check(type.BaseType);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (member is PropertyInfo property) Check(property.PropertyType);
                if (member is FieldInfo field) Check(field.FieldType);
                if (member is MethodInfo method) Check(method.ReturnType);
                if (member is MethodBase callable)
                    foreach (var parameter in callable.GetParameters()) Check(parameter.ParameterType);
            }
        }
        void Check(Type? type)
        {
            if (type is null || type.IsGenericParameter) return;
            if (type.HasElementType) { Check(type.GetElementType()); return; }
            Assert.True(type.IsVisible, type.FullName);
            Assert.True(type.Assembly == contracts || type.Assembly == typeof(FhirObject).Assembly ||
                type.Assembly.GetName().Name!.StartsWith("System."), type.FullName);
            foreach (var argument in type.GetGenericArguments()) Check(argument);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("package")]
    [InlineData("packageVersion")]
    [InlineData("policy")]
    [InlineData("models")]
    public void AnyIdentityMismatchIsRejectedBeforeAnEngineCanRun(string field)
    {
        var id = R4ProviderFixture.Identity;
        var wrong = new ModelIdentity(field == "version" ? "5.0.0" : id.FhirVersion,
            field == "package" ? "other.core" : id.PackageId,
            field == "packageVersion" ? "other-version" : id.PackageVersion,
            field == "policy" ? "other-policy" : id.PolicyId,
            field == "models" ? "other-models" : id.ModelId);
        var primitive = R4ProviderFixture.CreatePrimitives();
        var rules = R4ProviderFixture.CreateRules();
        var error = Assert.Throws<FhirSdkException>(() => new FhirModelContext(R4ProviderFixture.CreateMetadata(),
            new PrimitiveProviderDefinition(wrong, primitive.Definitions), rules));
        Assert.StartsWith("FHIR_CONTEXT_IDENTITY:", error.Message);
        Assert.Throws<FhirSdkException>(() => new FhirModelContext(R4ProviderFixture.CreateMetadata(), primitive,
            new ValidationProviderDefinition(wrong, rules.Capabilities, rules.Rules)));
    }

    [Fact]
    public void RelabelledForeignPrimitiveIsRejectedByExactInventory()
    {
        var primitives = R4ProviderFixture.CreatePrimitives();
        var first = primitives.Definitions.First(p => p.FhirTypeName == "string");
        var mixed = primitives.Definitions.Select(p => p == first ? p with { PrimitiveType = typeof(R5P.FhirString) } : p);
        var error = Assert.Throws<FhirSdkException>(() => new FhirModelContext(R4ProviderFixture.CreateMetadata(),
            new PrimitiveProviderDefinition(primitives.Identity, mixed), R4ProviderFixture.CreateRules()));
        Assert.StartsWith("FHIR_CONTEXT_PROVIDER:", error.Message);
    }

    [Fact]
    public void InventoriesAreCopiedAndCannotBeMutatedThroughThePublicLists()
    {
        var original = R4ProviderFixture.CreateMetadata();
        var types = original.ModelTypes.ToList();
        var resources = original.Resources.ToList();
        var metadata = new ModelMetadataDefinition(original.Identity, types, resources, original.ConcreteDataTypes,
            original.DeclaredDataTypes, original.ExtensionValues, original.OpenTypes);
        types.Clear(); resources.Clear();
        Assert.NotEmpty(metadata.ModelTypes);
        Assert.Throws<NotSupportedException>(() => ((IList<Type>)metadata.ModelTypes).Clear());
        var context = new FhirModelContext(metadata, R4ProviderFixture.CreatePrimitives(), R4ProviderFixture.CreateRules());
        Assert.IsType<R4R.Patient>(context.CreateResource("Patient"));
        Assert.All(typeof(ModelIdentity).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(FhirModelContext).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void SameResourceNamesResolveToDistinctTypesAndUnknownFactoriesFail()
    {
        var r4 = R4(); var r5 = R5ModelContext.Default;
        Assert.IsType<R4R.Patient>(r4.CreateResource("Patient"));
        Assert.IsType<R5R.Patient>(r5.CreateResource("Patient"));
        Assert.Throws<FhirSdkException>(() => r4.CreateResource("Organization"));
        Assert.Throws<FhirSdkException>(() => r4.CreateResource("Resource"));
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(r4).Parse<R5R.Patient>("{\"resourceType\":\"Patient\"}"));
        Assert.IsType<R4R.Patient>(new FhirJsonParser(r4).Parse<Resource>("{\"resourceType\":\"Patient\"}"));
    }

    [Fact]
    public void ProductionEnginesRoundTripBootstrapBackboneContainedAndPrimitiveMetadata()
    {
        const string json = """
            {"resourceType":"Patient","meta":{"tag":[{"code":"tag"}],"security":[{"code":"sec"}]},
             "text":{"status":"generated","div":"<div xmlns='http://www.w3.org/1999/xhtml'>text</div>"},
             "extension":[{"url":"https://example.org/quantity","valueQuantity":{"value":1.2300}}],
             "name":[{"given":["Ada",null],"_given":[null,{"extension":[{"url":"https://example.org/missing","valueString":"known"}]}]}],
             "contact":[{"name":{"family":"Contact"}}],"contained":[{"resourceType":"Patient","id":"nested"}]}
            """;
        var context = R4(); var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
        var patient = parser.Parse<R4R.Patient>(json);
        Assert.IsType<R4T.Coding>(patient.Meta!.Tag.Single());
        Assert.IsType<R4R.Patient>(patient.Contained.Single());
        Assert.IsType<R4T.Quantity>(patient.Extension.Single().Value);
        var output = serializer.Serialize(patient);
        Assert.Contains("1.2300", output);
        Assert.Equal(output, serializer.Serialize(parser.Parse<R4R.Patient>(output)));
        Assert.True(new FhirValidator(context).Validate(patient).IsValid);
    }

    [Fact]
    public void ReleaseSpecificPrimitiveTokensAndRulesStayIsolated()
    {
        const string r4Json = """{"resourceType":"Patient","photo":[{"size":12}]}""";
        var context = R4();
        var patient = new FhirJsonParser(context).Parse<R4R.Patient>(r4Json);
        Assert.Equal(12, patient.Photo.Single().Size!.Value);
        Assert.Contains("\"size\":12", new FhirJsonSerializer(context).Serialize(patient));
        Assert.ThrowsAny<Exception>(() => new FhirJsonParser(context).Parse<R4R.Patient>(r4Json.Replace("12", "\"12\"")));
        var r5 = new FhirJsonParser().Parse<R5R.Patient>(r4Json.Replace("12", "\"12\""));
        Assert.Contains("\"size\":\"12\"", new FhirJsonSerializer().Serialize(r5));
        var invalid = new R4R.Observation { ValueString = new R4P.FhirString("x"), ValueInteger = new R4P.FhirInteger(1) };
        var result = new FhirValidator(context).Validate(invalid);
        Assert.Contains(result.Issues, i => i.Code == ValidationIssueCode.Required && i.Path == "Observation.status");
        Assert.Contains(result.Issues, i => i.Code == ValidationIssueCode.ChoiceElement);
        Assert.All(result.Issues.Where(i => i.Code is ValidationIssueCode.Required or ValidationIssueCode.ChoiceElement), i => Assert.NotNull(i.RuleId));
        Assert.False(context.Capabilities.CompleteResourceSet);
        Assert.False(context.Capabilities.InvariantValidation);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("contained")]
    [InlineData("tag")]
    [InlineData("security")]
    [InlineData("emptyCoding")]
    [InlineData("extensionType")]
    [InlineData("extensionPrimitive")]
    public void ForeignObjectsAreRejectedEarlyByBothEnginesInBothDirections(string site)
    {
        var p4 = new R4R.Patient { Meta = new Meta() }; var p5 = new R5R.Patient { Meta = new Meta() };
        switch (site)
        {
            case "contained": p4.Contained.Add(new R5R.Patient()); p5.Contained.Add(new R4R.Patient()); break;
            case "tag": p4.Meta.Tag.Add(new R5T.Coding { Code = new R5P.FhirCode("foreign") }); p5.Meta.Tag.Add(new R4T.Coding { Code = new R4P.FhirCode("foreign") }); break;
            case "security": p4.Meta.Security.Add(new R5T.Coding()); p5.Meta.Security.Add(new R4T.Coding()); break;
            case "emptyCoding": p4.Meta.Tag.Add(new R5T.Coding()); p5.Meta.Tag.Add(new R4T.Coding()); break;
            case "extensionType": p4.Extension.Add(new Extension { Url = "x", Value = new R5T.Quantity() }); p5.Extension.Add(new Extension { Url = "x", Value = new R4T.Quantity() }); break;
            case "extensionPrimitive": p4.Extension.Add(new Extension { Url = "x", Value = new R5P.FhirString("foreign") }); p5.Extension.Add(new Extension { Url = "x", Value = new R4P.FhirString("foreign") }); break;
        }
        var r4 = R4(); var r5 = R5ModelContext.Default;
        Reject(r4, site == "root" ? p5 : p4); Reject(r5, site == "root" ? p4 : p5);
        if (site == "emptyCoding") Assert.IsType<R5T.Coding>(p4.Meta.Tag.Single());
        // Failed operations cannot poison either context.
        Assert.IsType<R4R.Patient>(r4.CreateResource("Patient"));
        Assert.IsType<R5R.Patient>(r5.CreateResource("Patient"));
        static void Reject(FhirModelContext context, Resource value)
        {
            var s = Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer(context).Serialize(value));
            var v = Assert.Throws<FhirSdkException>(() => new FhirValidator(context).Validate(value));
            Assert.StartsWith("FHIR_MODEL_OWNERSHIP:", s.Message);
            Assert.Equal(s.Message, v.Message);
        }
    }

    [Fact]
    public void FactoriesCannotReturnAnotherContextOrAnUnexpectedFhirName()
    {
        var original = R4ProviderFixture.CreateMetadata();
        var resources = original.Resources.Select(r => r.FhirTypeName == "Patient" ? r with { Factory = () => new R5R.Patient() } : r);
        var context = new FhirModelContext(new ModelMetadataDefinition(original.Identity, original.ModelTypes,
            resources, original.ConcreteDataTypes, original.DeclaredDataTypes, original.ExtensionValues),
            R4ProviderFixture.CreatePrimitives(), R4ProviderFixture.CreateRules());
        Assert.Throws<FhirSdkException>(() => context.CreateResource("Patient"));
        Assert.Throws<FhirSdkException>(() => new FhirJsonParser(context).Parse<Resource>("{\"resourceType\":\"Patient\"}"));
    }

    [Fact]
    public async Task ConcurrentAndInterleavedProductionContextsRemainIndependent()
    {
        var r4 = R4(); var r5 = R5ModelContext.Default;
        var parser4 = new FhirJsonParser(r4); var parser5 = new FhirJsonParser(r5);
        var serializer4 = new FhirJsonSerializer(r4); var serializer5 = new FhirJsonSerializer(r5);
        var validator4 = new FhirValidator(r4); var validator5 = new FhirValidator(r5);
        await Task.WhenAll(Enumerable.Range(0, 80).Select(i => Task.Run(() =>
        {
            var p4 = parser4.Parse<R4R.Patient>("{\"resourceType\":\"Patient\",\"id\":\"r4\"}");
            var p5 = parser5.Parse<R5R.Patient>("{\"resourceType\":\"Patient\",\"id\":\"r5\"}");
            Assert.True(validator4.Validate(p4).IsValid); Assert.True(validator5.Validate(p5).IsValid);
            Assert.Contains("r4", serializer4.Serialize(p4)); Assert.Contains("r5", serializer5.Serialize(p5));
            Assert.Throws<FhirSdkException>(() => serializer4.Serialize(p5));
        })));
    }
}
