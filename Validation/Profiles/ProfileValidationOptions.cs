namespace MyFhirSdk.Validation.Profiles;

/// <summary>
/// Controls profile validation framework behavior.
/// </summary>
public sealed class ProfileValidationOptions
{
    /// <summary>
    /// Default options for profile validation.
    /// </summary>
    public static ProfileValidationOptions Default { get; } = new();

    /// <summary>
    /// Behavior when an explicitly requested profile is not supported by any registered package.
    /// </summary>
    public UnknownProfileBehavior UnknownExplicitProfileBehavior { get; init; } = UnknownProfileBehavior.Error;

    /// <summary>
    /// Behavior when a resource-declared profile is not supported by any registered package.
    /// </summary>
    public UnknownProfileBehavior UnknownDeclaredProfileBehavior { get; init; } = UnknownProfileBehavior.Ignore;
}

/// <summary>
/// Determines how profile validation handles unsupported profile URLs.
/// </summary>
public enum UnknownProfileBehavior
{
    /// <summary>
    /// Do not emit an issue for unsupported profiles.
    /// </summary>
    Ignore,

    /// <summary>
    /// Emit a warning issue for unsupported profiles.
    /// </summary>
    Warning,

    /// <summary>
    /// Emit an error issue for unsupported profiles.
    /// </summary>
    Error
}
