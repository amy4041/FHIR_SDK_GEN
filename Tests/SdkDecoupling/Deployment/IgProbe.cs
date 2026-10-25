using MyFhirSdk.ImplementationGuides.TwCore;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;
using MyFhirSdk.R4.Resources;

internal static class IgProbe
{
    public static object Run()
    {
        var context = new FhirModelContext(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
        var parser = new FhirJsonParser(context);
        var validator = new ProfileValidator(new FhirValidator(context), TwCorePackage.Default);
        var negative = validator.Validate(new Patient(), TwCoreProfiles.Patient);
        var positive = validator.Validate(parser.Parse<Patient>("""{"resourceType":"Patient","identifier":[{"system":"https://example.test/id","value":"synthetic"}]}"""), TwCoreProfiles.Patient);
        if (negative.IsValid || !positive.IsValid || !negative.Issues.Any(i => i.PackageId == TwCorePackage.Default.PackageId))
            throw new InvalidOperationException("Deployed TW Core POC validation failed.");
        // Replace the package for this profile at composition, retaining the same SDK binary.
        var replacement = new ProfileValidator(new FhirValidator(context), new ReplacementPackage());
        if (!replacement.Validate(new Patient(), TwCoreProfiles.Patient).IsValid)
            throw new InvalidOperationException("Injected package replacement was ignored.");
        return new { positive = true, negative = true, replacement = true, fhirVersion = context.Identity.FhirVersion };
    }

    private sealed class ReplacementPackage : IImplementationGuidePackage
    {
        public string PackageId => "deployment.replacement#1";
        public string Name => "Synthetic replacement";
        public string FhirVersion => "4.0.1";
        public IReadOnlyCollection<string> SupportedProfiles => [TwCoreProfiles.Patient];
        public bool SupportsProfile(string profileUrl) => profileUrl == TwCoreProfiles.Patient;
        public bool SupportsResource(string profileUrl, Type resourceType) => SupportsProfile(profileUrl) && resourceType == typeof(Patient);
        public IEnumerable<IProfileValidationRule> GetRules(string profileUrl, Type resourceType) => [];
    }
}
