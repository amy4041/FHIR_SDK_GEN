namespace MyFhirSdk.Core;

internal interface IPrimitiveValueAccessor
{
    object? UntypedValue { get; }

    Type ValueType { get; }

    void SetUntypedValue(object? value);
}
