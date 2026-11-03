using System.Text.Json;

namespace MyFhirSdk.Primitives;

internal interface IPrimitiveDefinition
{
    string FhirTypeName { get; }

    Type PrimitiveType { get; }

    Type ValueType { get; }

    IPrimitiveCodec Codec { get; }

    IPrimitiveValidator Validator { get; }
}

internal interface IPrimitiveCodec : ModelContracts.IPrimitiveCodec
{
}

internal interface IPrimitiveValidator : ModelContracts.IPrimitiveValidator
{
}
