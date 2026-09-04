namespace MyFhirSdk.CodeGen.Mapping;

public sealed record CSharpTypeMapping(
    string FhirTypeCode,
    string TypeName,
    string CSharpTypeName,
    CSharpTypeCategory Category,
    string? RequiredUsing)
{
    public bool RequiresUsing => RequiredUsing is not null;
}
