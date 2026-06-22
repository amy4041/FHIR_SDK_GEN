using MyFhirSdk.Core;

namespace MyFhirSdk.Validation.Profiles;

/// <summary>
/// Runs base FHIR validation and optional implementation guide profile validation.
/// </summary>
public sealed class ProfileValidator
{
    private const string UnknownProfileRuleId = "PROFILE-UNKNOWN";

    private readonly IFhirValidator _baseValidator;
    private readonly ProfileValidationOptions _options;
    private readonly IReadOnlyList<IImplementationGuidePackage> _packages;

    /// <summary>
    /// Creates a profile validator with default options.
    /// </summary>
    public ProfileValidator(
        IFhirValidator baseValidator,
        params IImplementationGuidePackage[] packages)
        : this(baseValidator, ProfileValidationOptions.Default, packages)
    {
    }

    /// <summary>
    /// Creates a profile validator with explicit options.
    /// </summary>
    public ProfileValidator(
        IFhirValidator baseValidator,
        ProfileValidationOptions options,
        params IImplementationGuidePackage[] packages)
    {
        _baseValidator = baseValidator ?? throw new ArgumentNullException(nameof(baseValidator));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        ArgumentNullException.ThrowIfNull(packages);

        if (packages.Any(package => package is null))
        {
            throw new ArgumentException("Packages cannot contain null values.", nameof(packages));
        }

        _packages = packages.ToArray();
    }

    /// <summary>
    /// Validates a resource against one explicit profile URL.
    /// </summary>
    public ValidationResult Validate(Resource resource, string profileUrl)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (string.IsNullOrWhiteSpace(profileUrl))
        {
            throw new ArgumentException("Profile URL is required.", nameof(profileUrl));
        }

        return ValidateInternal(
            resource,
            [profileUrl],
            _options.UnknownExplicitProfileBehavior);
    }

    /// <summary>
    /// Validates a resource against one or more explicit profile URLs.
    /// </summary>
    public ValidationResult Validate(Resource resource, IEnumerable<string> profileUrls)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return ValidateInternal(
            resource,
            NormalizeProfileUrls(profileUrls, nameof(profileUrls)),
            _options.UnknownExplicitProfileBehavior);
    }

    /// <summary>
    /// Validates a resource against profile URLs declared in resource.meta.profile.
    /// </summary>
    public ValidationResult ValidateDeclaredProfiles(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return ValidateInternal(
            resource,
            NormalizeProfileUrls(resource.Meta?.Profile ?? Array.Empty<string>(), nameof(resource.Meta.Profile)),
            _options.UnknownDeclaredProfileBehavior);
    }

    private ValidationResult ValidateInternal(
        Resource resource,
        IReadOnlyList<string> profileUrls,
        UnknownProfileBehavior unknownProfileBehavior)
    {
        var issues = new List<ValidationIssue>(_baseValidator.Validate(resource).Issues);

        foreach (var profileUrl in profileUrls)
        {
            var package = FindPackage(profileUrl);
            if (package is null)
            {
                AddUnknownProfileIssue(resource, profileUrl, issues, unknownProfileBehavior);
                continue;
            }

            RunPackageRules(resource, profileUrl, package, issues);
        }

        return issues.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(issues);
    }

    private IImplementationGuidePackage? FindPackage(string profileUrl)
    {
        return _packages.FirstOrDefault(package => package.SupportsProfile(profileUrl));
    }

    private static void RunPackageRules(
        Resource resource,
        string profileUrl,
        IImplementationGuidePackage package,
        ICollection<ValidationIssue> issues)
    {
        var rules = package.GetRules(profileUrl, resource.GetType())
            ?? throw new InvalidOperationException(
                package.PackageId + " returned a null profile rule collection.");

        foreach (var rule in rules)
        {
            if (rule is null)
            {
                throw new InvalidOperationException(
                    package.PackageId + " returned a null profile validation rule.");
            }

            rule.Validate(
                new ProfileValidationContext
                {
                    Resource = resource,
                    PackageId = package.PackageId,
                    ProfileUrl = profileUrl,
                    RuleId = rule.RuleId
                },
                issues);
        }
    }

    private static IReadOnlyList<string> NormalizeProfileUrls(
        IEnumerable<string> profileUrls,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(profileUrls, parameterName);

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profileUrl in profileUrls)
        {
            if (string.IsNullOrWhiteSpace(profileUrl))
            {
                throw new ArgumentException(
                    "Profile URLs cannot contain null, empty, or whitespace values.",
                    parameterName);
            }

            if (seen.Add(profileUrl))
            {
                normalized.Add(profileUrl);
            }
        }

        return normalized;
    }

    private static void AddUnknownProfileIssue(
        Resource resource,
        string profileUrl,
        ICollection<ValidationIssue> issues,
        UnknownProfileBehavior behavior)
    {
        var severity = behavior switch
        {
            UnknownProfileBehavior.Ignore => (ValidationSeverity?)null,
            UnknownProfileBehavior.Warning => ValidationSeverity.Warning,
            UnknownProfileBehavior.Error => ValidationSeverity.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown profile behavior.")
        };

        if (severity is null)
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Path = resource.ResourceType + ".meta.profile",
            Code = ValidationIssueCode.Profile,
            Severity = severity.Value,
            Message = "No registered implementation guide package supports profile '" + profileUrl + "'.",
            Source = ValidationRuleSource.ImplementationGuide,
            ProfileUrl = profileUrl,
            RuleId = UnknownProfileRuleId
        });
    }
}
