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
    public void OnlyContextualCompositionOverloadsRemain()
    {
        var constructors = typeof(ProfileValidator).GetConstructors();
        Assert.Equal(2, constructors.Length);
        Assert.All(constructors, c => Assert.Equal(typeof(IContextualFhirValidator), c.GetParameters()[0].ParameterType));
        Assert.All(constructors, c => Assert.Null(c.GetCustomAttribute<ObsoleteAttribute>()));
    }

    [Fact]
    public void InternalRuleOnlyValidatorCannotPretendToHaveAR5Context()
    {
        var validator = new FhirValidator(TestContexts.R5.Rules, TestContexts.R5.Primitives, new FhirObjectGraphWalker());
        Assert.Throws<InvalidOperationException>(() => validator.Context);
        Assert.Throws<InvalidOperationException>(() => new ProfileValidator(validator));
    }
}
