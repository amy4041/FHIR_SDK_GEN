namespace MyFhirSdk.Tests.ImplementationGuides.TwCore;

public sealed class TwCorePackageTests
{
    [Fact]
    public void DefaultPackageExposesTwCoreMetadata()
    {
        var package = TwCorePackage.Default;

        Assert.Equal("tw.gov.mohw.twcore#1.0.0", package.PackageId);
        Assert.Equal("TW Core", package.Name);
        Assert.Equal("R4.0.1", package.FhirVersion);
        var profile = Assert.Single(package.SupportedProfiles);
        Assert.Equal(TwCoreProfiles.Patient, profile);
    }

    [Fact]
    public void SupportsTwCorePatientProfile()
    {
        var package = TwCorePackage.Default;

        Assert.True(package.SupportsProfile(TwCoreProfiles.Patient));
        Assert.False(package.SupportsProfile("https://example.org/fhir/StructureDefinition/unknown"));
    }

    [Fact]
    public void GetRulesReturnsInitialPatientRules()
    {
        var package = TwCorePackage.Default;

        var rules = package.GetRules(TwCoreProfiles.Patient, typeof(Patient)).ToList();

        Assert.Equal(
            ["TWCORE-PAT-002", "TWCORE-PAT-003", "TWCORE-PAT-004"],
            rules.Select(rule => rule.RuleId));
    }

    [Fact]
    public void GetRulesReturnsEmptyForUnsupportedResourceType()
    {
        var package = TwCorePackage.Default;

        var rules = package.GetRules(TwCoreProfiles.Patient, typeof(Organization));

        Assert.Empty(rules);
    }
}
