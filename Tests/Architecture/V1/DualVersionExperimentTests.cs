using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;
using R4P = MyFhirSdk.R4.Primitives;
using R4T = MyFhirSdk.R4.Types;
using R4R = MyFhirSdk.R4.Resources;
using R5P = MyFhirSdk.Primitives;
using R5T = MyFhirSdk.Types;
using R5R = MyFhirSdk.Resources;

namespace MyFhirSdk.Tests.Architecture.V1;

public sealed class DualVersionExperimentTests
{
    private const string PatientJson = """
        {"resourceType":"Patient","id":"v1","active":true,
         "meta":{"tag":[{"system":"urn:tag","code":"test"}],"security":[{"code":"safe"}]},
         "text":{"status":"generated","div":"<div xmlns=\"http://www.w3.org/1999/xhtml\">V1</div>"},
         "name":[{"family":"Test","given":["Alice",null,"C"],"_given":[null,{"id":"metadata"},null]}],
         "photo":[{"contentType":"image/png","size":42}],
         "extension":[{"url":"urn:value","valueString":"test"}],
         "contact":[{"name":{"family":"Contact"}}],
         "contained":[{"resourceType":"Observation","status":"final","code":{"text":"test"},"valueQuantity":{"value":1.20}}]}
        """;

    [Fact]
    public void SameProcessFactoriesHaveDifferentClrIdentitiesAndSharedRuntime()
    {
        var r4 = ExperimentContext.R4();
        var r5 = ExperimentContext.R5();
        Assert.IsType<R4R.Patient>(r4.CreateResource("Patient"));
        Assert.IsType<R5R.Patient>(r5.CreateResource("Patient"));
        Assert.IsType<R4R.Observation>(r4.CreateResource("Observation"));
        Assert.IsType<R5R.Observation>(r5.CreateResource("Observation"));
        Assert.NotEqual(typeof(R4R.Patient).Assembly, typeof(R5R.Patient).Assembly);
        Assert.IsAssignableFrom<Resource>(r4.CreateResource("Patient"));
        Assert.Equal("MyFhirSdk.Runtime", typeof(Resource).Assembly.GetName().Name);
        Assert.All(typeof(R4R.Patient).Assembly.GetReferencedAssemblies(), a =>
            Assert.True(a.Name == "MyFhirSdk.Runtime" || a.Name!.StartsWith("System.", StringComparison.Ordinal)));
        Assert.Throws<FhirSdkException>(() => r4.CreateResource("Unknown"));
        Assert.Throws<FhirSdkException>(() => r4.CreateResource("Resource"));
        Assert.Throws<FhirSdkException>(() => r4.CreateResource("Bundle"));
        Assert.IsType<R4R.Patient>(r4.Parse<Resource>("""{"resourceType":"Patient"}"""));
    }

    [Fact]
    public void R4RoundTripCoversBootstrapBackboneChoiceContainedAndPrimitiveMetadata()
    {
        var context = ExperimentContext.R4();
        var patient = context.Parse<R4R.Patient>(PatientJson);
        Assert.IsType<R4T.Coding>(patient.Meta!.Tag.Single());
        Assert.IsType<R4T.Coding>(patient.Meta.Security.Single());
        Assert.IsType<R4P.FhirString>(patient.Extension.Single().Value);
        Assert.Equal("Contact", patient.Contact.Single().Name!.Family!.Value);
        Assert.IsType<R4P.FhirUnsignedInt>(patient.Photo.Single().Size);
        Assert.IsType<R4R.Observation>(patient.Contained.Single());
        Assert.True(context.Validate(patient).IsValid);
        var serialized = context.Serialize(patient);
        var again = context.Parse<R4R.Patient>(serialized);
        Assert.Equal(serialized, context.Serialize(again));
        Assert.Equal(3, again.Name.Single().Given.Count);
        Assert.Equal("metadata", again.Name.Single().Given[1].Id);
        Assert.Null(again.Name.Single().Given[1].Value);
        Assert.Equal("C", again.Name.Single().Given[2].Value);
    }

    [Fact]
    public void R5RoundTripUsesItsOwnBootstrapCodingAndPrimitives()
    {
        var context = ExperimentContext.R5();
        var patient = context.Parse<R5R.Patient>(PatientJson.Replace("\"size\":42", "\"size\":\"42\""));
        Assert.IsType<R5T.Coding>(patient.Meta!.Tag.Single());
        Assert.IsType<R5T.Coding>(patient.Meta.Security.Single());
        Assert.IsType<R5P.FhirString>(patient.Extension.Single().Value);
        Assert.IsType<R5P.FhirInteger64>(patient.Photo.Single().Size);
        Assert.True(context.Validate(patient).IsValid);
        var json = context.Serialize(patient);
        Assert.Equal(json, context.Serialize(context.Parse<R5R.Patient>(json)));
    }

    [Theory]
    [InlineData("valueCoding", "{\"code\":\"test\"}", "Coding")]
    [InlineData("valueQuantity", "{\"value\":1.20,\"unit\":\"mg\"}", "Quantity")]
    [InlineData("valueDosage", "{\"text\":\"experiment\"}", "Dosage")]
    [InlineData("valueMeta", "{\"tag\":[{\"code\":\"nested\"}]}", "Meta")]
    [InlineData("valueRange", "{\"low\":{\"value\":1},\"high\":{\"value\":2}}", "Range")]
    public void ExtensionDatatypesAndAbstractBridgesRoundTrip(string wire, string valueJson, string typeName)
    {
        var context = ExperimentContext.R4();
        var json = "{\"resourceType\":\"Patient\",\"extension\":[{\"url\":\"urn:test\",\"" + wire + "\":" + valueJson + "}]}";
        var patient = context.Parse<R4R.Patient>(json);
        Assert.Equal(typeName, patient.Extension.Single().Value!.GetType().Name);
        Assert.True(context.Validate(patient).IsValid);
        var serialized = context.Serialize(patient);
        Assert.Equal(serialized, context.Serialize(context.Parse<R4R.Patient>(serialized)));
        if (typeName == "Range")
            Assert.IsType<R4T.SimpleQuantity>(Assert.IsType<R4T.Range>(patient.Extension.Single().Value).Low);
        if (typeName == "Meta")
            Assert.IsType<R4T.Coding>(Assert.IsType<Meta>(patient.Extension.Single().Value).Tag.Single());
    }

    [Fact]
    public void PrimitiveLexicalErrorsRemainInvalidInR4()
    {
        var context = ExperimentContext.R4();
        var patient = context.Parse<R4R.Patient>("""{"resourceType":"Patient","birthDate":"2026-99-99","photo":[{"data":"not-base64!"}]}""");
        Assert.Equal(2, context.Validate(patient).Issues.Count(i => i.Code == ValidationIssueCode.PrimitiveFormat));
        Assert.Throws<FhirSdkException>(() => context.Parse<R4R.Patient>("""{"resourceType":"Patient","active":"true"}"""));
    }

    [Fact]
    public void AttachmentSizeUsesUnsignedIntNumberInR4AndInteger64StringInR5()
    {
        var r4 = ExperimentContext.R4();
        var r5 = ExperimentContext.R5();
        var p4 = r4.Parse<R4R.Patient>("""{"resourceType":"Patient","photo":[{"size":2147483647}]}""");
        var p5 = r5.Parse<R5R.Patient>("""{"resourceType":"Patient","photo":[{"size":"2147483648"}]}""");
        Assert.Equal(int.MaxValue, p4.Photo.Single().Size!.Value);
        Assert.Equal(2147483648L, p5.Photo.Single().Size!.Value);
        using var j4 = JsonDocument.Parse(r4.Serialize(p4));
        using var j5 = JsonDocument.Parse(r5.Serialize(p5));
        Assert.Equal(JsonValueKind.Number, j4.RootElement.GetProperty("photo")[0].GetProperty("size").ValueKind);
        Assert.Equal(JsonValueKind.String, j5.RootElement.GetProperty("photo")[0].GetProperty("size").ValueKind);
        Assert.Throws<FhirSdkException>(() => r4.Parse<R4R.Patient>("""{"resourceType":"Patient","photo":[{"size":"1"}]}"""));
        Assert.Throws<FhirSdkException>(() => r4.Parse<R4R.Patient>("""{"resourceType":"Patient","photo":[{"size":2147483648}]}"""));
        Assert.Throws<FhirSdkException>(() => r5.Parse<R5R.Patient>("""{"resourceType":"Patient","photo":[{"size":1}]}"""));
        p4.Photo[0].Size = new R4P.FhirUnsignedInt(-1);
        Assert.Contains(r4.Validate(p4).Issues, i => i.Code == ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void OfficialR4RequiredChoiceAndCollectionRulesRunThroughExistingExecutor()
    {
        var context = ExperimentContext.R4();
        var missing = context.Validate(new R4R.Observation());
        Assert.Contains(missing.Issues, i => i.Path == "Observation.status" && i.Code == ValidationIssueCode.Required);
        Assert.Contains(missing.Issues, i => i.Path == "Observation.code" && i.Code == ValidationIssueCode.Required);
        var observation = context.Parse<R4R.Observation>("""{"resourceType":"Observation","status":"final","code":{"text":"x"},"valueString":"a","valueBoolean":true}""");
        Assert.Contains(context.Validate(observation).Issues, i => i.Code == ValidationIssueCode.ChoiceElement);
        var valid = context.Parse<R4R.Observation>("""{"resourceType":"Observation","status":"final","code":{"text":"x"},"valueString":"a"}""");
        Assert.True(context.Validate(valid).IsValid);
        valid.Component.Add(new R4R.ObservationComponent());
        Assert.Contains(context.Validate(valid).Issues, i => i.Path == "Observation.component[0].code");
        var patient = new R4R.Patient { Name = null! };
        Assert.Contains(context.Validate(patient).Issues, i => i.Code == ValidationIssueCode.Cardinality);
        patient = new R4R.Patient { Text = new Narrative() };
        Assert.Contains(context.Validate(patient).Issues, i => i.Path == "Patient.text.div");
        patient.Extension.Add(new Extension());
        Assert.Contains(context.Validate(patient).Issues, i => i.Path == "Patient.extension[0].url");
    }

    [Fact]
    public void CrossVersionRootsParseTargetsAndMixedProvidersAreRejected()
    {
        var r4 = ExperimentContext.R4();
        var r5 = ExperimentContext.R5();
        Assert.Throws<FhirSdkException>(() => r4.Serialize(new R5R.Patient()));
        Assert.Throws<FhirSdkException>(() => r5.Serialize(new R4R.Patient()));
        Assert.Throws<FhirSdkException>(() => r4.Validate(new R5R.Patient()));
        Assert.Throws<FhirSdkException>(() => r5.Validate(new R4R.Patient()));
        Assert.Throws<FhirSdkException>(() => r4.Parse<R5R.Patient>("""{"resourceType":"Patient"}"""));
        Assert.Throws<FhirSdkException>(() => r5.Parse<R4R.Patient>("""{"resourceType":"Patient"}"""));
        Assert.Throws<FhirSdkException>(ExperimentContext.MixedForNegativeTest);
    }

    [Theory]
    [InlineData("contained")]
    [InlineData("tag")]
    [InlineData("security")]
    [InlineData("extensionDatatype")]
    [InlineData("extensionPrimitive")]
    public void CrossVersionNestedObjectsAreRejectedBeforeSerializerAndValidator(string site)
    {
        var r4 = ExperimentContext.R4();
        var r5 = ExperimentContext.R5();
        var p4 = new R4R.Patient { Meta = new Meta() };
        var p5 = new R5R.Patient { Meta = new Meta() };
        switch (site)
        {
            case "contained": p4.Contained.Add(new R5R.Patient()); p5.Contained.Add(new R4R.Patient()); break;
            case "tag": p4.Meta.Tag.Add(new R5T.Coding()); p5.Meta.Tag.Add(new R4T.Coding()); break;
            case "security": p4.Meta.Security.Add(new R5T.Coding()); p5.Meta.Security.Add(new R4T.Coding()); break;
            case "extensionDatatype":
                p4.Extension.Add(new Extension { Url = "urn:x", Value = new R5T.Quantity() });
                p5.Extension.Add(new Extension { Url = "urn:x", Value = new R4T.Quantity() }); break;
            case "extensionPrimitive":
                p4.Extension.Add(new Extension { Url = "urn:x", Value = new R5P.FhirString("x") });
                p5.Extension.Add(new Extension { Url = "urn:x", Value = new R4P.FhirString("x") }); break;
        }
        Assert.Throws<FhirSdkException>(() => r4.Serialize(p4));
        Assert.Throws<FhirSdkException>(() => r4.Validate(p4));
        Assert.Throws<FhirSdkException>(() => r5.Serialize(p5));
        Assert.Throws<FhirSdkException>(() => r5.Validate(p5));
        // Failed operations have not stripped/replaced the foreign object.
        if (site == "contained") Assert.IsType<R5R.Patient>(p4.Contained.Single());
    }

    [Fact]
    public void RawR5EnginesNeedTheV2NestedOwnershipGuard()
    {
        // Evidence of a production gap, not a claim that the current engines
        // already enforce contexts. The isolated boundary above closes it.
        var patient = new R5R.Patient { Meta = new Meta() };
        patient.Meta.Tag.Add(new R4T.Coding { Code = new R4P.FhirCode("foreign") });
        Assert.Throws<KeyNotFoundException>(() => new FhirJsonSerializer().Serialize(patient));
        var emptyForeign = new R4T.Coding();
        patient.Meta.Tag[0] = emptyForeign;
        new FhirJsonSerializer().Serialize(patient); // Empty foreign datatype is silently omitted.
        Assert.Throws<FhirSdkException>(() => ExperimentContext.R5().Serialize(patient));
    }

    [Fact]
    public async Task ConcurrentAndInterleavedContextsDoNotShareFactoriesRulesOrPrimitiveCaches()
    {
        var r4 = ExperimentContext.R4();
        var r5 = ExperimentContext.R5();
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() =>
        {
            if (i % 2 == 0)
            {
                var p = r4.Parse<R4R.Patient>(PatientJson);
                Assert.True(r4.Validate(p).IsValid);
                Assert.IsType<R4R.Patient>(r4.Parse<R4R.Patient>(r4.Serialize(p)));
            }
            else
            {
                var p = r5.Parse<R5R.Patient>(PatientJson.Replace("\"size\":42", "\"size\":\"42\""));
                Assert.True(r5.Validate(p).IsValid);
                Assert.IsType<R5R.Patient>(r5.Parse<R5R.Patient>(r5.Serialize(p)));
            }
        })));
    }
}
