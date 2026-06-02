namespace MyFhirSdk.Validation;

/// <summary>
/// Machine-readable validation issue category.
/// </summary>
public enum ValidationIssueCode
{
    Required,
    Cardinality,
    PrimitiveFormat,
    ChoiceElement
}
