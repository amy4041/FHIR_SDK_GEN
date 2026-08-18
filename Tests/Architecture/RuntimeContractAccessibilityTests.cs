using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.Serialization;
using MyFhirSdk.Validation;

namespace MyFhirSdk.Tests.Architecture;

public sealed class RuntimeContractAccessibilityTests
{
    private static readonly string[] ForbiddenExportedTypeNames =
    [
        "IFhirValidatablePrimitive",
        "IPrimitiveValueAccessor",
        "IPrimitiveDefinition",
        "IPrimitiveCodec",
        "IPrimitiveValidator",
        "PrimitiveRegistry",
        "PrimitiveDefinition",
        "PrimitiveValidator",
        "PrimitiveValueAccess",
        "PrimitiveCodecs",
        "PrimitiveValidators",
        "StandardPrimitiveCodec",
        "LiteralPrimitiveCodec"
    ];

    [Fact]
    public void RuntimeImplementationTypesAreNotExported()
    {
        var exportedTypes = typeof(FhirObject).Assembly.GetExportedTypes();

        Assert.All(
            ForbiddenExportedTypeNames,
            forbiddenName => Assert.DoesNotContain(
                exportedTypes,
                type => string.Equals(
                    type.Name,
                    forbiddenName,
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void ValidatorContractRemainsResourceScoped()
    {
        AssertResourceValidationSignature(typeof(IFhirValidator));
        AssertResourceValidationSignature(typeof(FhirValidator));
    }

    private static void AssertResourceValidationSignature(Type validatorType)
    {
        var declaredPublicValidateMethods = validatorType
            .GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(IFhirValidator.Validate))
            .ToArray();

        var validateMethod = Assert.Single(declaredPublicValidateMethods);
        Assert.Equal(typeof(ValidationResult), validateMethod.ReturnType);

        var parameter = Assert.Single(validateMethod.GetParameters());
        Assert.Equal(typeof(Resource), parameter.ParameterType);
    }
}
