using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

// These calls compile in a separate assembly without production friend access.
public sealed class PrimitiveAccessorIntegrationTests
{
    [Fact]
    public void NullableValueCanBeReadWrittenAndClearedWithoutClearingMetadata()
    {
        var primitive = new FhirBoolean(true) { Id = "retained" };
        IPrimitiveValueAccessor accessor = primitive;
        Assert.Equal(typeof(bool?), accessor.ValueType);
        Assert.Equal(true, accessor.UntypedValue);
        accessor.SetUntypedValue(false);
        Assert.Equal(false, primitive.Value);
        accessor.SetUntypedValue(null);
        Assert.Null(primitive.Value);
        Assert.False(primitive.HasValue);
        Assert.Equal("retained", primitive.Id);
    }

    [Fact]
    public void ReferenceValueCanBeClearedAndDoesNotValidateFhirFormat()
    {
        var primitive = new FhirDate("2026-10-06");
        IPrimitiveValueAccessor accessor = primitive;
        Assert.Equal(typeof(string), accessor.ValueType);
        accessor.SetUntypedValue("not a date");
        Assert.Equal("not a date", primitive.Value);
        accessor.SetUntypedValue(null);
        Assert.Null(accessor.UntypedValue);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(1)]
    public void WrongTypeIsRejectedWithoutChangingValue(object value)
    {
        var primitive = new FhirBoolean(true);
        IPrimitiveValueAccessor accessor = primitive;
        Assert.Throws<InvalidCastException>(() => accessor.SetUntypedValue(value));
        Assert.Equal(true, primitive.Value);
    }

    [Fact]
    public void NumericValuesAreNotCoerced()
    {
        IPrimitiveValueAccessor accessor = new FhirInteger(7);
        Assert.Throws<InvalidCastException>(() => accessor.SetUntypedValue(8L));
        Assert.Equal(7, accessor.UntypedValue);
    }

    [Fact]
    public void NonNullableValueRejectsNullAndSupportsExternalSubclass()
    {
        var primitive = new NonNullablePrimitive { Value = 7 };
        IPrimitiveValueAccessor accessor = primitive;
        Assert.Equal(typeof(int), accessor.ValueType);
        Assert.Throws<NullReferenceException>(() => accessor.SetUntypedValue(null));
        Assert.Equal(7, primitive.Value);
        accessor.SetUntypedValue(8);
        Assert.Equal(8, primitive.Value);
    }

    private sealed class NonNullablePrimitive : PrimitiveType<int>;
}
