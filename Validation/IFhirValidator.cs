using MyFhirSdk.Core;

namespace MyFhirSdk.Validation;

/// <summary>
/// Validates FHIR resources and returns structured validation issues.
/// </summary>
public interface IFhirValidator
{
    /// <summary>
    /// Validates a resource.
    /// </summary>
    ValidationResult Validate(Resource resource);
}
