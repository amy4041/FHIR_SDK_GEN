using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class KernelPrimitiveRegistrationTests
{
    [Fact]
    public void AccessorImplementationAloneCannotRegisterAsFhirPrimitive()
    {
        Assert.Throws<ArgumentException>(() => new PrimitiveDefinition("external",
            typeof(AccessorOnly), typeof(string), PrimitiveCodecs.String, PrimitiveValidators.String));
        Assert.Throws<KeyNotFoundException>(() => PrimitiveRegistry.Default.GetRequired(typeof(AccessorOnly)));
    }

    [Fact]
    public void ExternalPrimitiveSubclassMustDeclareMatchingValueTypeAndIsNotAutomaticallyRegistered()
    {
        Assert.Throws<ArgumentException>(() => new PrimitiveDefinition("external",
            typeof(ExternalPrimitive), typeof(int?), PrimitiveCodecs.String, PrimitiveValidators.String));
        Assert.Throws<KeyNotFoundException>(() => PrimitiveRegistry.Default.GetRequired(typeof(ExternalPrimitive)));
        var definition = new PrimitiveDefinition("external", typeof(ExternalPrimitive), typeof(string),
            PrimitiveCodecs.String, PrimitiveValidators.String);
        var registry = PrimitiveRegistry.Create([definition]);
        Assert.Same(definition, registry.GetRequired(typeof(ExternalPrimitive)));
        Assert.Same(definition, registry.GetRequired("external"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(8)]
    [InlineData(8L)]
    [InlineData("8")]
    public void PublicAccessorPreservesOriginalDirectCastResultsAndFailures(object? value)
    {
        CompareCast<int>(7, value);
        CompareCast<int?>(7, value);
        CompareCast<string>("original", value);
    }

    private static void CompareCast<T>(T initial, object? value)
    {
        var originalValue = initial;
        var originalFailure = Record.Exception(() => originalValue = (T?)value!);
        var primitive = new TestPrimitive<T> { Value = initial, Id = "retained" };
        IPrimitiveValueAccessor accessor = primitive;
        var accessorFailure = Record.Exception(() => accessor.SetUntypedValue(value));
        Assert.Equal(originalFailure?.GetType(), accessorFailure?.GetType());
        Assert.Equal(originalValue, primitive.Value);
        Assert.Equal(typeof(T), accessor.ValueType);
        Assert.Equal("retained", primitive.Id);
    }

    private sealed class TestPrimitive<T> : PrimitiveType<T>;
    private sealed class ExternalPrimitive : PrimitiveType<string>;

    private sealed class AccessorOnly : IPrimitiveValueAccessor
    {
        public object? UntypedValue { get; private set; }
        public Type ValueType => typeof(string);
        public void SetUntypedValue(object? value) => UntypedValue = value;
    }
}
