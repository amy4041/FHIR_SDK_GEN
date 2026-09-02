namespace MyFhirSdk.CodeGen.Ir;

public sealed record ModelPropertyIr(
    string FhirName,
    string JsonName,
    string CSharpName,
    string? CSharpType,
    bool IsNullable,
    bool IsCollection,
    ModelTypeReferenceIr? Type);
