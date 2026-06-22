using MyFhirSdk.Validation.Profiles;

namespace MyFhirSdk.Tests.Validation.Profiles;

public sealed class ProfileValidatorTests
{
    private const string TestProfileUrl = "https://example.org/fhir/StructureDefinition/test-patient";

    [Fact]
    public void ValidateRejectsNullResource()
    {
        var validator = new ProfileValidator(new RecordingBaseValidator());

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, TestProfileUrl));
    }

    [Fact]
    public void ValidateRejectsEmptyProfileUrl()
    {
        var validator = new ProfileValidator(new RecordingBaseValidator());

        Assert.Throws<ArgumentException>(() => validator.Validate(new Patient(), string.Empty));
    }

    [Fact]
    public void ValidateRunsBaseValidationOnceAndProfileRule()
    {
        var patient = new Patient { Id = "invalid/id" };
        var baseIssue = new ValidationIssue
        {
            Path = "Patient.id",
            Code = ValidationIssueCode.PrimitiveFormat,
            Severity = ValidationSeverity.Error,
            Message = "Patient.id is invalid."
        };
        var baseValidator = new RecordingBaseValidator(new ValidationResult([baseIssue]));
        var rule = new RecordingProfileRule();
        var package = new StubPackage([TestProfileUrl], rule);
        var validator = new ProfileValidator(baseValidator, package);

        var result = validator.Validate(patient, TestProfileUrl);

        Assert.Equal(1, baseValidator.CallCount);
        Assert.Same(patient, baseValidator.LastResource);
        Assert.Equal(1, package.GetRulesCallCount);
        Assert.Equal(typeof(Patient), package.LastResourceType);
        Assert.Equal(1, rule.CallCount);
        Assert.NotNull(rule.LastContext);
        Assert.Same(patient, rule.LastContext.Resource);
        Assert.Equal(package.PackageId, rule.LastContext.PackageId);
        Assert.Equal(TestProfileUrl, rule.LastContext.ProfileUrl);
        Assert.Equal(rule.RuleId, rule.LastContext.RuleId);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Issues.Count);
        Assert.Same(baseIssue, result.Issues[0]);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, result.Issues[1].Source);
        Assert.Equal(package.PackageId, result.Issues[1].PackageId);
        Assert.Equal(TestProfileUrl, result.Issues[1].ProfileUrl);
        Assert.Equal(rule.RuleId, result.Issues[1].RuleId);
    }

    [Fact]
    public void ValidateUnknownExplicitProfileReturnsErrorByDefault()
    {
        var baseValidator = new RecordingBaseValidator();
        var validator = new ProfileValidator(baseValidator);

        var result = validator.Validate(new Patient(), TestProfileUrl);

        Assert.Equal(1, baseValidator.CallCount);
        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("Patient.meta.profile", issue.Path);
        Assert.Equal(ValidationIssueCode.Profile, issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, issue.Source);
        Assert.Null(issue.PackageId);
        Assert.Equal(TestProfileUrl, issue.ProfileUrl);
        Assert.Equal("PROFILE-UNKNOWN", issue.RuleId);
    }

    [Fact]
    public void ValidateCanIgnoreUnknownExplicitProfile()
    {
        var baseValidator = new RecordingBaseValidator();
        var options = new ProfileValidationOptions
        {
            UnknownExplicitProfileBehavior = UnknownProfileBehavior.Ignore
        };
        var validator = new ProfileValidator(baseValidator, options);

        var result = validator.Validate(new Patient(), TestProfileUrl);

        Assert.Equal(1, baseValidator.CallCount);
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateMultipleProfilesDeduplicatesAndRunsBaseOnce()
    {
        var baseValidator = new RecordingBaseValidator();
        var rule = new RecordingProfileRule();
        var package = new StubPackage([TestProfileUrl], rule);
        var validator = new ProfileValidator(baseValidator, package);

        var result = validator.Validate(new Patient(), [TestProfileUrl, TestProfileUrl]);

        Assert.False(result.IsValid);
        Assert.Equal(1, baseValidator.CallCount);
        Assert.Equal(1, package.GetRulesCallCount);
        Assert.Equal(1, rule.CallCount);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void ValidateDeclaredProfilesUsesMetaProfile()
    {
        var patient = new Patient
        {
            Meta = new Meta
            {
                Profile = [TestProfileUrl, TestProfileUrl]
            }
        };
        var baseValidator = new RecordingBaseValidator();
        var rule = new RecordingProfileRule();
        var package = new StubPackage([TestProfileUrl], rule);
        var validator = new ProfileValidator(baseValidator, package);

        var result = validator.ValidateDeclaredProfiles(patient);

        Assert.False(result.IsValid);
        Assert.Equal(1, baseValidator.CallCount);
        Assert.Equal(1, package.GetRulesCallCount);
        Assert.Equal(1, rule.CallCount);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(TestProfileUrl, issue.ProfileUrl);
    }

    [Fact]
    public void ValidateDeclaredProfilesWithoutMetaRunsBaseOnly()
    {
        var baseIssue = new ValidationIssue
        {
            Path = "Patient.id",
            Code = ValidationIssueCode.PrimitiveFormat,
            Severity = ValidationSeverity.Error,
            Message = "Patient.id is invalid."
        };
        var baseValidator = new RecordingBaseValidator(new ValidationResult([baseIssue]));
        var validator = new ProfileValidator(baseValidator);

        var result = validator.ValidateDeclaredProfiles(new Patient());

        Assert.Equal(1, baseValidator.CallCount);
        var issue = Assert.Single(result.Issues);
        Assert.Same(baseIssue, issue);
    }

    private sealed class RecordingBaseValidator : IFhirValidator
    {
        private readonly ValidationResult _result;

        public RecordingBaseValidator()
            : this(ValidationResult.Success)
        {
        }

        public RecordingBaseValidator(ValidationResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Resource? LastResource { get; private set; }

        public ValidationResult Validate(Resource resource)
        {
            CallCount++;
            LastResource = resource;
            return _result;
        }
    }

    private sealed class StubPackage : IImplementationGuidePackage
    {
        private readonly IReadOnlyList<IProfileValidationRule> _rules;

        public StubPackage(
            IReadOnlyCollection<string> supportedProfiles,
            params IProfileValidationRule[] rules)
        {
            SupportedProfiles = supportedProfiles;
            _rules = rules;
        }

        public string PackageId => "test.package#1.0.0";

        public string Name => "Test Package";

        public string FhirVersion => "R5";

        public IReadOnlyCollection<string> SupportedProfiles { get; }

        public int GetRulesCallCount { get; private set; }

        public Type? LastResourceType { get; private set; }

        public bool SupportsProfile(string profileUrl)
        {
            return SupportedProfiles.Contains(profileUrl, StringComparer.Ordinal);
        }

        public IEnumerable<IProfileValidationRule> GetRules(
            string profileUrl,
            Type resourceType)
        {
            GetRulesCallCount++;
            LastResourceType = resourceType;
            return _rules;
        }
    }

    private sealed class RecordingProfileRule : IProfileValidationRule
    {
        public string RuleId => "TEST-PAT-001";

        public int CallCount { get; private set; }

        public ProfileValidationContext? LastContext { get; private set; }

        public void Validate(
            ProfileValidationContext context,
            ICollection<ValidationIssue> issues)
        {
            CallCount++;
            LastContext = context;
            issues.Add(new ValidationIssue
            {
                Path = context.Resource.ResourceType + ".profileRule",
                Code = ValidationIssueCode.Profile,
                Severity = ValidationSeverity.Error,
                Message = "Test profile rule failed.",
                Source = ValidationRuleSource.ImplementationGuide,
                PackageId = context.PackageId,
                ProfileUrl = context.ProfileUrl,
                RuleId = context.RuleId
            });
        }
    }
}
