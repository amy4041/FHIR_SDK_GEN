namespace MyFhirSdk.CodeGen.Mapping;

public sealed record CSharpNameConversionResult(
    string? Name,
    CSharpNameConversionFailure Failure)
{
    public bool IsSuccess => Failure == CSharpNameConversionFailure.None;
}
