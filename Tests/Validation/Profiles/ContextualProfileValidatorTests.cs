using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Validation.Profiles;
using R4Resources = MyFhirSdk.R4.Resources;
using R5 = MyFhirSdk.Resources;

namespace MyFhirSdk.Tests.Validation.Profiles;

// Synthetic IGs exercise the framework independently of the concrete TW Core POC.
public sealed class ContextualProfileValidatorTests
{
    private const string Profile = "https://example.test/StructureDefinition/d1-patient";
    private static FhirModelContext Context(bool r5) => r5
        ? new(R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation)
        : new(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
    private static Resource Patient(bool r5) => r5 ? new R5.Patient() : new R4Resources.Patient();
    private static Type PatientType(bool r5) => r5 ? typeof(R5.Patient) : typeof(R4Resources.Patient);
    private static TestPackage Package(bool r5, params IProfileValidationRule[] rules) =>
        new(r5 ? "5.0.0" : "4.0.1", PatientType(r5), rules);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealValidatorExposesItsExactContextAndMergesBaseAndIgIssues(bool r5)
    {
        var context = Context(r5);
        IContextualFhirValidator validator = new FhirValidator(context);
        Assert.Same(context, validator.Context);
        var patient = Patient(r5);
        patient.Id = "invalid/id";
        var result = new ProfileValidator(validator, Package(r5, new TestRule("RULE-1"))).Validate(patient, Profile);
        Assert.Contains(result.Issues, i => i.Source == ValidationRuleSource.BaseFhir && i.Path == "Patient.id");
        var ig = Assert.Single(result.Issues, i => i.Source == ValidationRuleSource.ImplementationGuide);
        Assert.Equal("synthetic.d1#9.2.3", ig.PackageId);
        Assert.Equal(Profile, ig.ProfileUrl);
        Assert.Equal("RULE-1", ig.RuleId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomValidatorAndPackageReplacementUseOnlySelectedImplementations(bool r5)
    {
        var baseIssue = new ValidationIssue { Path = "Patient.id", Message = "custom base issue" };
        var validator = new RecordingValidator(Context(r5), new ValidationResult([baseIssue]));
        var first = new ProfileValidator(validator, Package(r5, new TestRule("FIRST")));
        var second = new ProfileValidator(validator, Package(r5, new TestRule("SECOND")));
        var a = first.Validate(Patient(r5), Profile);
        var b = second.Validate(Patient(r5), Profile);
        Assert.Same(baseIssue, a.Issues[0]);
        Assert.Equal("FIRST", a.Issues[1].RuleId);
        Assert.Equal("SECOND", b.Issues[1].RuleId);
        Assert.Equal(2, validator.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VersionMismatchIsRejectedBeforeRuleEnumeration(bool r5)
    {
        var package = Package(!r5);
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(Context(r5)), package));
        Assert.Equal(0, package.RuleRequests);
    }

    [Theory]
    [InlineData("R4.0.1")]
    [InlineData("4.0")]
    [InlineData("04.0.1")]
    [InlineData("4.0.1-preview")]
    [InlineData("4.0.1\n")]
    [InlineData("")]
    [InlineData(null)]
    public void NoncanonicalBaseVersionsAreNotNormalized(string? version)
    {
        var package = new TestPackage(version!, typeof(R4Resources.Patient), []);
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(Context(false)), package));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateProfileRegistrationsAreRejectedRegardlessOfOrder(bool reverse)
    {
        var a = Package(false, new TestRule("A"));
        var b = Package(false, new TestRule("B"));
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(Context(false)),
            reverse ? [b, a] : [a, b]));
        a.Profiles = [Profile, Profile];
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(Context(false)), a));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownUnsupportedResourceIsErrorEvenWhenUnknownProfilesAreIgnored(bool r5)
    {
        var package = Package(r5);
        var validator = new RecordingValidator(Context(r5));
        var composed = new ProfileValidator(validator, new ProfileValidationOptions
        {
            UnknownExplicitProfileBehavior = UnknownProfileBehavior.Ignore,
            UnknownDeclaredProfileBehavior = UnknownProfileBehavior.Ignore
        }, package);
        Resource observation = r5 ? new R5.Observation() : new R4Resources.Observation();
        observation.Meta = new Meta { Profile = [Profile] };
        foreach (var result in new[] { composed.Validate(observation, Profile), composed.ValidateDeclaredProfiles(observation) })
        {
            var issue = Assert.Single(result.Issues);
            Assert.Equal("PROFILE-RESOURCE-UNSUPPORTED", issue.RuleId);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal(Profile, issue.ProfileUrl);
            Assert.Equal(package.PackageId, issue.PackageId);
        }
        Assert.Equal(0, package.RuleRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeclaredSupportedResourceWithNoExtraRulesStillRunsBaseValidation(bool r5)
    {
        var validator = new RecordingValidator(Context(r5));
        var package = Package(r5);
        Assert.True(new ProfileValidator(validator, package).Validate(Patient(r5), Profile).IsValid);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(1, package.RuleRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnershipIsCheckedBeforeCustomValidatorOrRules(bool r5)
    {
        var validator = new RecordingValidator(Context(r5));
        var package = Package(r5, new TestRule("NEVER"));
        var composed = new ProfileValidator(validator, package);
        Assert.Throws<FhirSdkException>(() => composed.Validate(Patient(!r5), Profile));
        var mixed = Patient(r5);
        if (mixed is R5.Patient p5) p5.Contained = [Patient(false)];
        else ((R4Resources.Patient)mixed).Contained = [Patient(true)];
        Assert.Throws<FhirSdkException>(() => composed.Validate(mixed, Profile));
        Assert.Equal(0, validator.Calls);
        Assert.Equal(0, package.RuleRequests);
    }

    [Theory]
    [InlineData(UnknownProfileBehavior.Ignore, 0)]
    [InlineData(UnknownProfileBehavior.Warning, 1)]
    [InlineData(UnknownProfileBehavior.Error, 1)]
    public void UnknownProfileOptionsApplyToExplicitAndDeclaredProfiles(UnknownProfileBehavior behavior, int count)
    {
        var validator = new ProfileValidator(new FhirValidator(Context(false)), new ProfileValidationOptions
        {
            UnknownExplicitProfileBehavior = behavior, UnknownDeclaredProfileBehavior = behavior
        });
        var patient = Patient(false);
        patient.Meta = new Meta { Profile = [Profile] };
        foreach (var result in new[] { validator.Validate(patient, Profile), validator.ValidateDeclaredProfiles(patient) })
        {
            Assert.Equal(count, result.Issues.Count);
            if (count > 0)
            {
                Assert.Equal("PROFILE-UNKNOWN", result.Issues[0].RuleId);
                Assert.Equal(behavior == UnknownProfileBehavior.Warning ? ValidationSeverity.Warning : ValidationSeverity.Error,
                    result.Issues[0].Severity);
            }
        }
        var defaults = new ProfileValidator(new FhirValidator(Context(false)));
        Assert.False(defaults.Validate(patient, Profile).IsValid);
        Assert.True(defaults.ValidateDeclaredProfiles(patient).IsValid);
    }

    [Fact]
    public void DuplicateDeclaredProfilesRunBaseAndRuleOnce()
    {
        var validator = new RecordingValidator(Context(false));
        var package = Package(false, new TestRule("ONCE"));
        var patient = Patient(false);
        patient.Meta = new Meta { Profile = [Profile, Profile] };
        Assert.Single(new ProfileValidator(validator, package).ValidateDeclaredProfiles(patient).Issues);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(1, package.RuleRequests);
    }

    [Fact]
    public void VersionQualifiedProfilesAreDiagnosedEvenWithIgnorePolicy()
    {
        var package = Package(false, new TestRule("NEVER"));
        var validator = new ProfileValidator(new FhirValidator(Context(false)), new ProfileValidationOptions
        {
            UnknownExplicitProfileBehavior = UnknownProfileBehavior.Ignore,
            UnknownDeclaredProfileBehavior = UnknownProfileBehavior.Ignore
        }, package);
        var patient = Patient(false);
        patient.Meta = new Meta { Profile = [Profile + "|1.0.0"] };
        foreach (var result in new[] { validator.Validate(patient, Profile + "|1.0.0"), validator.ValidateDeclaredProfiles(patient) })
            Assert.Equal("PROFILE-VERSION-UNSUPPORTED", Assert.Single(result.Issues).RuleId);
        Assert.Equal(0, package.RuleRequests);
    }

    [Fact]
    public void InvalidRuleCollectionsAndEntriesAreConfigurationFailures()
    {
        var validator = new FhirValidator(Context(false));
        foreach (var rules in new IProfileValidationRule[]?[] { null, [null!], [new TestRule("")] })
        {
            var package = Package(false);
            package.Rules = rules;
            var composed = new ProfileValidator(validator, package);
            Assert.Throws<InvalidOperationException>(() => composed.Validate(Patient(false), Profile));
        }
    }

    [Fact]
    public void ContextualCompositionRejectsMissingDependenciesAndInvalidOptions()
    {
        var validator = new FhirValidator(Context(false));
        Assert.Throws<ArgumentNullException>(() => new ProfileValidator((IContextualFhirValidator)null!));
        Assert.Throws<ArgumentNullException>(() => new ProfileValidator(validator, (ProfileValidationOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new ProfileValidator(validator, (IImplementationGuidePackage[])null!));
        Assert.Throws<ArgumentException>(() => new ProfileValidator(validator, [null!]));
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new RecordingValidator(null!)));
        Assert.Throws<ArgumentException>(() => new ProfileValidator(validator,
            new ProfileValidationOptions { UnknownDeclaredProfileBehavior = (UnknownProfileBehavior)99 }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://example.test/p|1")]
    public void InvalidDeclaredProfileKeysAreRejectedAtConstruction(string? profile)
    {
        var package = Package(false);
        package.Profiles = [profile!];
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(Context(false)), package));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PositiveResourceWithSupportedProfilePassesRealBaseValidation(bool r5)
    {
        var package = Package(r5);
        Assert.True(new ProfileValidator(new FhirValidator(Context(r5)), package).Validate(Patient(r5), Profile).IsValid);
        Assert.Equal(1, package.RuleRequests);
    }

    private sealed class RecordingValidator(FhirModelContext context, ValidationResult? result = null) : IContextualFhirValidator
    {
        public FhirModelContext Context { get; } = context;
        public int Calls { get; private set; }
        public ValidationResult Validate(Resource resource) { Calls++; return result ?? ValidationResult.Success; }
    }

    private sealed class TestPackage(string version, Type resourceType, IProfileValidationRule[] rules) : IImplementationGuidePackage
    {
        public string PackageId => "synthetic.d1#9.2.3";
        public string Name => "D1 test package";
        public string FhirVersion => version;
        public IReadOnlyCollection<string> Profiles { get; set; } = [Profile];
        public IReadOnlyCollection<string> SupportedProfiles => Profiles;
        public IProfileValidationRule[]? Rules { get; set; } = rules;
        public int RuleRequests { get; private set; }
        public bool SupportsProfile(string profileUrl) => Profiles.Contains(profileUrl, StringComparer.Ordinal);
        public bool SupportsResource(string profileUrl, Type type) => SupportsProfile(profileUrl) && type == resourceType;
        public IEnumerable<IProfileValidationRule> GetRules(string profileUrl, Type type) { RuleRequests++; return Rules!; }
    }

    private sealed class TestRule(string id) : IProfileValidationRule
    {
        public string RuleId => id;
        public void Validate(ProfileValidationContext context, ICollection<ValidationIssue> issues) =>
            issues.Add(new ValidationIssue { Path = "Patient.test", Code = ValidationIssueCode.Profile, Message = "synthetic failure" });
    }
}
