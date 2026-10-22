using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Tests.ImplementationGuides.TwCore.Validation;
using MyFhirSdk.ModelMetadata.R5;

namespace MyFhirSdk.Tests.ImplementationGuides.TwCore.D2;

public sealed class R4TwCoreCompositionTests
{
    private static FhirModelContext Context() => TwCorePatientValidationTests.CreateContext();
    private static ProfileValidator Validator() => new(new FhirValidator(Context()), TwCorePackage.Default);

    [Fact]
    public void TwCoreTypesAreOwnedOnlyByTheIndependentIgAssembly()
    {
        var sdk = typeof(FhirValidator).Assembly;
        var ig = typeof(TwCorePackage).Assembly;
        Assert.Equal("MyFhirSdk.ImplementationGuides.TwCore", ig.GetName().Name);
        Assert.NotEqual(sdk, ig);
        Assert.Equal(ig, typeof(TwCoreProfiles).Assembly);
        Assert.All(ig.GetTypes().Where(t => t.FullName!.StartsWith("MyFhirSdk.ImplementationGuides.TwCore.")),
            type => Assert.Null(sdk.GetType(type.FullName!)));
        Assert.DoesNotContain(sdk.GetReferencedAssemblies(), a => a.Name == ig.GetName().Name);
        Assert.DoesNotContain(ig.GetReferencedAssemblies(), a => a.Name == "MyFhirSdk.R5.Models");
        Assert.Contains(ig.GetReferencedAssemblies(), a => a.Name == "MyFhirSdk.R4.Models");
        Assert.Equal("MyFhirSdk.R4.Models", typeof(Patient).Assembly.GetName().Name);
        Assert.Equal(typeof(Patient).Assembly, typeof(Identifier).Assembly);
        Assert.Equal(typeof(Patient).Assembly, typeof(FhirString).Assembly);
    }

    [Fact]
    public void PackageSupportsOnlyItsRegisteredR4PatientPair()
    {
        var package = TwCorePackage.Default;
        Assert.True(package.SupportsResource(TwCoreProfiles.Patient, typeof(Patient)));
        Assert.False(package.SupportsResource(TwCoreProfiles.Patient, typeof(MyFhirSdk.Resources.Patient)));
        Assert.False(package.SupportsResource(TwCoreProfiles.Patient, typeof(Organization)));
        Assert.False(package.SupportsResource("https://example.test/unknown", typeof(Patient)));
        Assert.Empty(package.GetRules(TwCoreProfiles.Patient, typeof(MyFhirSdk.Resources.Patient)));
        Assert.Empty(package.GetRules("https://example.test/unknown", typeof(Patient)));
    }

    [Fact]
    public void R5ContextCannotComposeTheR4Package()
    {
        var r5 = new FhirModelContext(R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation);
        Assert.Throws<ArgumentException>(() => new ProfileValidator(new FhirValidator(r5), TwCorePackage.Default));
    }

    [Fact]
    public void R5PatientIsRejectedByTheR4Composition()
    {
        Assert.Throws<FhirSdkException>(() =>
            Validator().Validate(new MyFhirSdk.Resources.Patient(), TwCoreProfiles.Patient));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void LegacyOverloadsRejectR5PatientForEveryProfileSelection(bool withOptions, int selection)
    {
        IFhirValidator baseValidator = new FhirValidator(new FhirModelContext(
            R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation));
        var validator = LegacyValidator(baseValidator, withOptions);
        var patient = new MyFhirSdk.Resources.Patient { Meta = new Meta { Profile = [TwCoreProfiles.Patient] } };
        var result = selection switch
        {
            0 => validator.Validate(patient, TwCoreProfiles.Patient),
            1 => validator.Validate(patient, new[] { TwCoreProfiles.Patient }),
            _ => validator.ValidateDeclaredProfiles(patient)
        };
        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("PROFILE-RESOURCE-UNSUPPORTED", issue.RuleId);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationIssueCode.Profile, issue.Code);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, issue.Source);
        Assert.Equal("tw.gov.mohw.twcore#1.0.0", issue.PackageId);
        Assert.Equal(TwCoreProfiles.Patient, issue.ProfileUrl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyOverloadsStillExecuteRulesForSupportedR4Patient(bool withOptions)
    {
        var validator = LegacyValidator(new FhirValidator(Context()), withOptions);
        var missing = validator.Validate(new Patient(), TwCoreProfiles.Patient);
        Assert.Equal("TWCORE-PAT-002", Assert.Single(missing.Issues).RuleId);
        var valid = new Patient { Identifier = [new Identifier
        {
            System = new FhirUri("https://example.test/id"), Value = new FhirString("123")
        }] };
        Assert.True(validator.Validate(valid, TwCoreProfiles.Patient).IsValid);
    }

    // Exercise the transition APIs explicitly until D3 removes them.
#pragma warning disable CS0618
    private static ProfileValidator LegacyValidator(IFhirValidator baseValidator, bool withOptions) => withOptions
        ? new ProfileValidator(baseValidator, new ProfileValidationOptions
        {
            UnknownExplicitProfileBehavior = UnknownProfileBehavior.Ignore,
            UnknownDeclaredProfileBehavior = UnknownProfileBehavior.Ignore
        }, TwCorePackage.Default)
        : new ProfileValidator(baseValidator, TwCorePackage.Default);
#pragma warning restore CS0618

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExtensionOnlyPrimitiveCountsAsPresentAfterR4RoundTrip(bool system)
    {
        var identifier = new Identifier { System = new FhirUri("https://example.test/id"), Value = new FhirString("123") };
        PrimitiveType<string> primitive = system ? new FhirUri() : new FhirString();
        primitive.Extension.Add(new Extension { Url = "https://example.test/absent", Value = new FhirString("withheld") });
        if (system) identifier.System = (FhirUri)primitive;
        else identifier.Value = (FhirString)primitive;
        var patient = new Patient { Identifier = [identifier] };
        var json = new FhirJsonSerializer(Context()).Serialize(patient);
        var parsed = new FhirJsonParser(Context()).Parse<Patient>(json);
        Assert.True(Validator().Validate(parsed, TwCoreProfiles.Patient).IsValid);
        var restored = system ? (PrimitiveType<string>)parsed.Identifier[0].System! : parsed.Identifier[0].Value!;
        Assert.False(restored.HasValue);
        Assert.Single(restored.Extension);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EmptyPrimitiveWithoutExtensionDoesNotCountAsPresent(bool system)
    {
        var identifier = new Identifier { System = new FhirUri("https://example.test/id"), Value = new FhirString("123") };
        if (system) identifier.System = new FhirUri();
        else identifier.Value = new FhirString();
        var result = Validator().Validate(new Patient { Identifier = [identifier] }, TwCoreProfiles.Patient);
        var issue = Assert.Single(result.Issues, i => i.Source == ValidationRuleSource.ImplementationGuide);
        Assert.Equal(system ? "TWCORE-PAT-003" : "TWCORE-PAT-004", issue.RuleId);
    }

    [Fact]
    public void DeclaredProfileMergesBaseAndIgIssuesWithOriginalProvenance()
    {
        var patient = new Patient { Id = "invalid/id", Meta = new Meta { Profile = [TwCoreProfiles.Patient, TwCoreProfiles.Patient] } };
        var result = Validator().ValidateDeclaredProfiles(patient);
        Assert.Contains(result.Issues, i => i.Source == ValidationRuleSource.BaseFhir && i.Path == "Patient.id");
        var ig = Assert.Single(result.Issues, i => i.Source == ValidationRuleSource.ImplementationGuide);
        Assert.Equal("TWCORE-PAT-002", ig.RuleId);
        Assert.Equal("tw.gov.mohw.twcore#1.0.0", ig.PackageId);
        Assert.Equal(TwCoreProfiles.Patient, ig.ProfileUrl);
    }

    [Fact]
    public void NullIdentifierEntriesDoNotSatisfyTheRequiredIdentifierRule()
    {
        var patient = new Patient { Identifier = [null!] };
        Assert.Contains(Validator().Validate(patient, TwCoreProfiles.Patient).Issues,
            i => i.Source == ValidationRuleSource.ImplementationGuide && i.RuleId == "TWCORE-PAT-002");
    }
}
