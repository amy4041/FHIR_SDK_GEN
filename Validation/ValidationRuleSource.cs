namespace MyFhirSdk.Validation;

/// <summary>
/// Identifies which validation layer produced an issue.
/// </summary>
public enum ValidationRuleSource
{
    /// <summary>
    /// Rule comes from the base FHIR SDK validation layer.
    /// </summary>
    BaseFhir,

    /// <summary>
    /// Rule comes from a concrete Implementation Guide or profile.
    /// </summary>
    ImplementationGuide,

    /// <summary>
    /// Rule comes from project, workflow, or application-specific validation.
    /// </summary>
    BusinessRule
}
