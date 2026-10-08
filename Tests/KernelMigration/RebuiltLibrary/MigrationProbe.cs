using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;

namespace KernelMigration.RebuiltLibrary;

// Expose moved types across a real library -> application boundary.
public static class MigrationProbe
{
    public static Base RoundTrip()
    {
        var parser = new FhirJsonParser();
        var serializer = new FhirJsonSerializer();
        var patient = parser.Parse<Patient>("""{"resourceType":"Patient","id":"k3-consumer","active":true}""");
        Base model = patient;
        if (model is not Resource || patient.Active?.Value != true)
            throw new Exception("Model/base assignment failed.");
        var roundTrip = parser.Parse<Patient>(serializer.Serialize(patient));
        if (roundTrip.Id != "k3-consumer" || roundTrip.Active?.Value != true)
            throw new Exception("Public parser/serializer round-trip failed.");
        return roundTrip;
    }

    public static PrimitiveType<bool?> AccessPrimitive()
    {
        PrimitiveType<bool?> primitive = new FhirBoolean { Value = true, Id = "primitive-id" };
        var accessor = (IPrimitiveValueAccessor)primitive;
        if (accessor.ValueType != typeof(bool?) || !Equals(accessor.UntypedValue, true))
            throw new Exception("Accessor type/read failed.");
        accessor.SetUntypedValue(false);
        if (primitive.Value != false) throw new Exception("Accessor write failed.");
        try { accessor.SetUntypedValue("false"); throw new Exception("Accessor accepted an invalid cast."); }
        catch (InvalidCastException) { }
        if (primitive.Value != false || primitive.Id != "primitive-id")
            throw new Exception("Failed cast changed primitive value/metadata.");
        accessor.SetUntypedValue(null);
        if (primitive.Value is not null || primitive.Id != "primitive-id")
            throw new Exception("Nullable accessor assignment failed.");
        IPrimitiveValueAccessor integer = new IntegerPrimitive { Value = 7 };
        try { integer.SetUntypedValue(null); throw new Exception("Non-nullable accessor accepted null."); }
        catch (NullReferenceException) { }
        if (integer.ValueType != typeof(int) || !Equals(integer.UntypedValue, 7))
            throw new Exception("Non-nullable accessor cast changed state.");
        return primitive;
    }

    private sealed class IntegerPrimitive : PrimitiveType<int> { }
}
