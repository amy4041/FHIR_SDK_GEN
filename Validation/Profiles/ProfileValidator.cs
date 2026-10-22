using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using System.Text.RegularExpressions;

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
    private readonly FhirModelContext? _context;
    private readonly IReadOnlyDictionary<string, IImplementationGuidePackage>? _profiles;

    /// <summary>Creates a version-checked IG composition with default options.</summary>
    public ProfileValidator(IContextualFhirValidator baseValidator,
        params IImplementationGuidePackage[] packages)
        : this(baseValidator, ProfileValidationOptions.Default, packages) { }

    /// <summary>Creates a version-checked IG composition with explicit options.</summary>
    public ProfileValidator(IContextualFhirValidator baseValidator, ProfileValidationOptions options,
        params IImplementationGuidePackage[] packages)
        : this(baseValidator, options, packages, contextual: true) { }

    /// <summary>
    /// Creates a profile validator with default options.
    /// </summary>
    [Obsolete("D1 transition only; removed in D3. Use IContextualFhirValidator for version-checked IG composition.")]
    public ProfileValidator(
        IFhirValidator baseValidator,
        params IImplementationGuidePackage[] packages)
        : this(baseValidator, ProfileValidationOptions.Default, packages, contextual: false)
    {
    }

    /// <summary>
    /// Creates a profile validator with explicit options.
    /// </summary>
    [Obsolete("D1 transition only; removed in D3. Use IContextualFhirValidator for version-checked IG composition.")]
    public ProfileValidator(
        IFhirValidator baseValidator,
        ProfileValidationOptions options,
        params IImplementationGuidePackage[] packages)
        : this(baseValidator, options, packages, contextual: false) { }

    private ProfileValidator(IFhirValidator baseValidator, ProfileValidationOptions options,
        IImplementationGuidePackage[] packages, bool contextual)
    {
        _baseValidator = baseValidator ?? throw new ArgumentNullException(nameof(baseValidator));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        ArgumentNullException.ThrowIfNull(packages);

        if (packages.Any(package => package is null))
        {
            throw new ArgumentException("Packages cannot contain null values.", nameof(packages));
        }

        _packages = packages.ToArray();
        if (!contextual) return;

        _context = ((IContextualFhirValidator)baseValidator).Context
            ?? throw new ArgumentException("The validator must expose its actual model context.", nameof(baseValidator));
        if (!IsBaseVersion(_context.Identity.FhirVersion))
            throw new ArgumentException("The context must use a canonical FHIR base version.", nameof(baseValidator));
        if (!Enum.IsDefined(options.UnknownExplicitProfileBehavior) || !Enum.IsDefined(options.UnknownDeclaredProfileBehavior))
            throw new ArgumentException("Unknown profile behavior is invalid.", nameof(options));

        var profiles = new Dictionary<string, IImplementationGuidePackage>(StringComparer.Ordinal);
        foreach (var package in _packages)
        {
            if (!IsBaseVersion(package.FhirVersion) || package.FhirVersion != _context.Identity.FhirVersion)
                throw new ArgumentException($"Package '{package.PackageId}' FHIR base version '{package.FhirVersion}' does not match context '{_context.Identity.FhirVersion}'.", nameof(packages));
            if (string.IsNullOrWhiteSpace(package.PackageId) || package.SupportedProfiles is null)
                throw new ArgumentException("Packages must declare an ID and supported profiles.", nameof(packages));
            foreach (var profile in package.SupportedProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile) || profile.Contains('|') || !package.SupportsProfile(profile))
                    throw new ArgumentException("Packages must declare unversioned, nonempty, supported profile URLs.", nameof(packages));
                if (!profiles.TryAdd(profile, package))
                    throw new ArgumentException($"Profile '{profile}' is registered more than once.", nameof(packages));
            }
        }
        _profiles = profiles;
    }

    private static bool IsBaseVersion(string? value) => value is not null && Regex.IsMatch(value,
        @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant);

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
        _context?.EnsureOwnership(resource);
        var issues = new List<ValidationIssue>(_baseValidator.Validate(resource).Issues);

        foreach (var profileUrl in profileUrls)
        {
            if (_context is not null && profileUrl.Contains('|'))
            {
                AddProfileIssue(resource, profileUrl, null, "PROFILE-VERSION-UNSUPPORTED",
                    "Version-qualified profile canonicals are not supported.", issues);
                continue;
            }
            var package = FindPackage(profileUrl);
            if (package is null)
            {
                AddUnknownProfileIssue(resource, profileUrl, issues, unknownProfileBehavior);
                continue;
            }

            if (!package.SupportsResource(profileUrl, resource.GetType()))
            {
                AddProfileIssue(resource, profileUrl, package, "PROFILE-RESOURCE-UNSUPPORTED",
                    "The registered profile does not support resource CLR type '" + resource.GetType().FullName + "'.", issues);
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
        if (_profiles is not null)
            return _profiles.TryGetValue(profileUrl, out var package) ? package : null;
        return _packages.FirstOrDefault(package => package.SupportsProfile(profileUrl));
    }

    private static void AddProfileIssue(Resource resource, string profile, IImplementationGuidePackage? package,
        string ruleId, string message, ICollection<ValidationIssue> issues) => issues.Add(new ValidationIssue
        {
            Path = resource.ResourceType + ".meta.profile", Code = ValidationIssueCode.Profile,
            Severity = ValidationSeverity.Error, Message = message, Source = ValidationRuleSource.ImplementationGuide,
            PackageId = package?.PackageId, ProfileUrl = profile, RuleId = ruleId
        });

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

            if (string.IsNullOrWhiteSpace(rule.RuleId))
                throw new InvalidOperationException("Profile rules must declare a nonempty rule ID.");
            var ruleIssues = new List<ValidationIssue>();
            rule.Validate(
                new ProfileValidationContext
                {
                    Resource = resource,
                    PackageId = package.PackageId,
                    ProfileUrl = profileUrl,
                    RuleId = rule.RuleId
                },
                ruleIssues);
            foreach (var issue in ruleIssues)
            {
                if (issue is null) throw new InvalidOperationException("Profile rules cannot return null issues.");
                issues.Add(new ValidationIssue
                {
                    Path = issue.Path, Message = issue.Message, Severity = issue.Severity, Code = issue.Code,
                    Source = ValidationRuleSource.ImplementationGuide, PackageId = package.PackageId,
                    ProfileUrl = profileUrl, RuleId = rule.RuleId
                });
            }
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
