using MyFhirSdk.ModelMetadata;

namespace MyFhirSdk.Validation;

/// <summary>A validator bound to one immutable model context for IG composition.</summary>
public interface IContextualFhirValidator : IFhirValidator
{
    /// <summary>The model context actually used by this validator.</summary>
    FhirModelContext Context { get; }
}
