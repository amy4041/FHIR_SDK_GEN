using MyFhirSdk.Serialization.Json;

namespace MyFhirSdk.Tests.ImplementationGuides.TwCore.Validation;

public sealed class KernelTwCoreMigrationTests
{
    [Fact]
    public void ProfileRulesStillTraverseRuntimeMetaAndSdkIdentifiersAfterRoundTrip()
    {
        var patient = new Patient
        {
            Meta = new Meta { Profile = { TwCoreProfiles.Patient } },
            Identifier = { new Identifier { System = new FhirUri("https://www.moi.gov.tw/"), Value = new FhirString("A123456789") } }
        };
        var context = TwCorePatientValidationTests.CreateContext();
        var parser = new FhirJsonParser(context);
        var serializer = new FhirJsonSerializer(context);
        patient = parser.Parse<Patient>(serializer.Serialize(patient));
        Assert.NotEqual(typeof(Meta).Assembly, typeof(Identifier).Assembly);
        var validator = new ProfileValidator(new FhirValidator(context), TwCorePackage.Default);
        Assert.True(validator.Validate(patient, TwCoreProfiles.Patient).IsValid);
        ((IPrimitiveValueAccessor)patient.Identifier[0].Value!).SetUntypedValue(null);
        patient = parser.Parse<Patient>(serializer.Serialize(patient));
        Assert.True(new FhirValidator(context).Validate(patient).IsValid);
        var issue = Assert.Single(validator.Validate(patient, TwCoreProfiles.Patient).Issues);
        Assert.Equal("Patient.identifier[0].value", issue.Path);
        Assert.Equal("TWCORE-PAT-004", issue.RuleId);
        Assert.Equal(ValidationRuleSource.ImplementationGuide, issue.Source);
        Assert.Equal("tw.gov.mohw.twcore#1.0.0", issue.PackageId);
        Assert.Equal(TwCoreProfiles.Patient, issue.ProfileUrl);
    }
}
