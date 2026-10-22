using System.Reflection;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;
using MyFhirSdk.Validation.Traversal;

namespace MyFhirSdk.Tests.Architecture.D1;

public sealed class ContextualValidatorContractTests
{
    [Fact]
    public void GeneralValidatorInterfaceIsUnchangedAndContextualInterfaceIsReadOnly()
    {
        Assert.Empty(typeof(IFhirValidator).GetProperties());
        Assert.Contains(typeof(IFhirValidator), typeof(IContextualFhirValidator).GetInterfaces());
        var property = Assert.Single(typeof(IContextualFhirValidator).GetProperties());
        Assert.Equal("Context", property.Name);
        Assert.Null(property.SetMethod);
        Assert.Null(typeof(FhirValidator).GetProperty("Context")!.SetMethod);
    }

    [Fact]
    public void OnlyTheTwoLegacyCompositionOverloadsAreObsolete()
    {
        var constructors = typeof(ProfileValidator).GetConstructors();
        Assert.Equal(4, constructors.Length);
        foreach (var constructor in constructors)
        {
            var legacy = constructor.GetParameters()[0].ParameterType == typeof(IFhirValidator);
            var obsolete = constructor.GetCustomAttribute<ObsoleteAttribute>();
            Assert.Equal(legacy, obsolete is not null);
            if (legacy) Assert.Contains("removed in D3", obsolete!.Message);
        }
    }

    [Fact]
    public void InternalRuleOnlyValidatorCannotPretendToHaveAR5Context()
    {
        var validator = new FhirValidator(R5ModelMetadataProvider.Default, new FhirObjectGraphWalker());
        Assert.Throws<InvalidOperationException>(() => validator.Context);
        Assert.Throws<InvalidOperationException>(() => new ProfileValidator(validator));
    }
}
