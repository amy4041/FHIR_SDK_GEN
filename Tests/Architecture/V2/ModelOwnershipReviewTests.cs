using System.Collections;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Tests.FhirVersions.V2;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Tests.Architecture.V2;

public sealed class ModelOwnershipReviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignEnumerableResourcesCannotBypassOwnershipAtRootOrContained(bool r5)
    {
        var context = r5 ? TestContexts.R5 : ModelContextTests.R4();
        var foreign = new EnumerableResource();
        Reject(context, foreign, "EnumerableResource");
        var patient = (DomainResource)context.CreateResource("Patient");
        patient.Contained.Add(foreign);
        Reject(context, patient, "Patient.contained[0]");
        Assert.Same(foreign, patient.Contained.Single());
        Assert.Equal(0, foreign.EnumerationCount);
        Assert.IsAssignableFrom<Resource>(context.CreateResource("Patient"));
    }

    [Theory]
    [InlineData("missing-tag")]
    [InlineData("missing-security")]
    [InlineData("missing-both")]
    [InlineData("wrong-tag")]
    [InlineData("wrong-both")]
    public void SharedMetaMappingsMustExistAndUseTheRegisteredCodingType(string mode)
    {
        var original = R4ProviderFixture.CreateMetadata();
        var entries = original.DeclaredDataTypes.Where(d => mode switch
        {
            "missing-tag" => d.PropertyName != "tag",
            "missing-security" => d.PropertyName != "security",
            "missing-both" => d.DeclaringType != typeof(Meta),
            _ => true
        }).Select(d => mode.StartsWith("wrong", StringComparison.Ordinal) && d.DeclaringType == typeof(Meta) &&
            (mode == "wrong-both" || d.PropertyName == "tag")
                ? d with { ConcreteType = typeof(MyFhirSdk.R4.Types.Quantity) } : d);
        var metadata = new ModelMetadataDefinition(original.Identity, original.ModelTypes, original.Resources,
            original.ConcreteDataTypes, entries, original.ExtensionValues, original.OpenTypes);
        var error = Assert.Throws<FhirSdkException>(() => new FhirModelContext(metadata,
            R4ProviderFixture.CreatePrimitives(), R4ProviderFixture.CreateRules()));
        Assert.StartsWith("FHIR_CONTEXT_PROVIDER:", error.Message);
        Assert.Contains("Meta.", error.Message);
        Assert.Contains("Coding", error.Message);
        var valid = ModelContextTests.R4();
        var patient = new MyFhirSdk.R4.Resources.Patient { Meta = new Meta() };
        patient.Meta.Tag.Add(new MyFhirSdk.R4.Types.Coding());
        Assert.True(new FhirValidator(valid).Validate(patient).IsValid);
        patient.Meta.Tag[0] = new MyFhirSdk.R4.Types.Quantity();
        Reject(valid, patient, "Patient.meta.tag[0]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonIgnoredForeignModelsAreRejectedWithTheSameEarlyDiagnostic(bool list)
    {
        var context = ContextWithReviewResource();
        var foreign = new MyFhirSdk.Primitives.FhirString("foreign");
        var resource = new ReviewResource();
        if (list) resource.HiddenItems.Add(foreign); else resource.Hidden = foreign;
        Reject(context, resource, list ? "ReviewResource.hiddenItems[0]" : "ReviewResource.hidden");
        if (list) resource.HiddenItems.Clear(); else resource.Hidden = null;
        resource.Hidden = new MyFhirSdk.R4.Primitives.FhirString("owned");
        Assert.Equal("{\"resourceType\":\"ReviewResource\"}", new FhirJsonSerializer(context).Serialize(resource));
        Assert.True(new FhirValidator(context).Validate(resource).IsValid);
    }

    [Fact]
    public void RegisteredEnumerableModelsStillTraverseTheirFhirPropertiesAndRules()
    {
        var context = ContextWithReviewResource();
        var resource = new ReviewResource { Child = new EnumerableResource() };
        Assert.True(new FhirValidator(context).Validate(resource).IsValid);
        resource.Child.Extension.Add(new Extension { Url = "x", Value = new MyFhirSdk.Primitives.FhirString("foreign") });
        Reject(context, resource, "ReviewResource.child.extension[0].value");
        Assert.Equal(0, resource.Child.EnumerationCount);
    }

    private static FhirModelContext ContextWithReviewResource()
    {
        var original = R4ProviderFixture.CreateMetadata();
        var metadata = new ModelMetadataDefinition(original.Identity,
            original.ModelTypes.Concat([typeof(ReviewResource), typeof(EnumerableResource)]),
            original.Resources.Concat([
                new ResourceDefinition("ReviewResource", typeof(ReviewResource), () => new ReviewResource()),
                new ResourceDefinition("EnumerableResource", typeof(EnumerableResource), () => new EnumerableResource())]),
            original.ConcreteDataTypes, original.DeclaredDataTypes, original.ExtensionValues, original.OpenTypes);
        return new FhirModelContext(metadata, R4ProviderFixture.CreatePrimitives(), R4ProviderFixture.CreateRules());
    }

    private static void Reject(FhirModelContext context, Resource resource, string path)
    {
        var serializer = Assert.Throws<FhirSdkException>(() => new FhirJsonSerializer(context).Serialize(resource));
        var validator = Assert.Throws<FhirSdkException>(() => new FhirValidator(context).Validate(resource));
        Assert.StartsWith("FHIR_MODEL_OWNERSHIP:", serializer.Message);
        Assert.Equal(serializer.Message, validator.Message);
        Assert.Contains(path, serializer.Message);
    }

    public sealed class EnumerableResource : DomainResource, IEnumerable
    {
        public override string ResourceType => "EnumerableResource";
        [JsonIgnore] public int EnumerationCount { get; private set; }
        public IEnumerator GetEnumerator()
        {
            EnumerationCount++;
            return Array.Empty<object>().GetEnumerator();
        }
    }

    public sealed class ReviewResource : Resource
    {
        public override string ResourceType => "ReviewResource";
        [JsonIgnore] public DataType? Hidden { get; set; }
        [JsonIgnore] public IList<DataType> HiddenItems { get; set; } = new List<DataType>();
        [JsonIgnore] public EnumerableResource? Child { get; set; }
    }
}
