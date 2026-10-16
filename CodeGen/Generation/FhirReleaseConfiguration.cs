namespace MyFhirSdk.CodeGen.Generation;

/// <summary>Approved release identities and compile destinations. No ambient current release.</summary>
public sealed class FhirReleaseConfiguration
{
    private FhirReleaseConfiguration(string release, string version, string packageSha256,
        string primitiveNamespace, string typeNamespace, string resourceNamespace,
        string compilerSourceVersion, string compilerSourceSha256)
    {
        Release = release; FhirVersion = version; PackageSha256 = packageSha256;
        PrimitiveNamespace = primitiveNamespace; TypeNamespace = typeNamespace; ResourceNamespace = resourceNamespace;
        CompilerSourceVersion = compilerSourceVersion; CompilerSourceSha256 = compilerSourceSha256;
    }

    public static FhirReleaseConfiguration R5 { get; } = new("R5", "5.0.0",
        "74b27cd1bfce9e80eaceac431edf230b0945a443564fbf5512f82e5fa50a80d4", "MyFhirSdk.Primitives", "MyFhirSdk.Types", "MyFhirSdk.Resources",
        "simple-quantity-source-v1", "321d5cd03d6ec3f2e70a26e608f088563c1f4d7c3ad4e680488d47f0e0affde8");
    public static FhirReleaseConfiguration R4 { get; } = new("R4", "4.0.1",
        "b090bf929e1f665cf2c91583720849695bc38d2892a7c5037c56cb00817fb091",
        "MyFhirSdk.R4.Primitives", "MyFhirSdk.R4.Types", "MyFhirSdk.R4.Resources",
        "simple-quantity-r4-source-v1", "8e0d53e95a66f618eeb12860a3f2ddee0dea9d1515c0b63607250fdeaf4ab2a2");

    public static FhirReleaseConfiguration ForVersion(string version) => version switch
    {
        "5.0.0" => R5, "4.0.1" => R4,
        _ => throw new ArgumentException($"Unsupported FHIR version '{version}'; expected 4.0.1 or 5.0.0.", nameof(version))
    };

    public string Release { get; }
    public string FhirVersion { get; }
    public string PackageId => $"hl7.fhir.{Release.ToLowerInvariant()}.core";
    public string PackageSha256 { get; }
    public string PackageType => Release == "R4" ? "fhir.core" : "Core";
    public string PrimitiveNamespace { get; }
    public string TypeNamespace { get; }
    public string ResourceNamespace { get; }
    public string GeneratedRoot => $"Generated/{Release}";
    public string MetadataNamespace => $"MyFhirSdk.ModelMetadata.{Release}";
    public string MetadataClass => $"Generated{Release}ModelMetadata";
    public string ValidationClass => $"Generated{Release}ValidationRules";
    public string MetadataPath => $"{GeneratedRoot}/ModelMetadata/{Release}ModelMetadata.g.cs";
    public string ValidationPath => $"{GeneratedRoot}/ModelMetadata/{Release}ValidationRules.g.cs";
    public string CompilerSourceVersion { get; }
    public string CompilerSourceSha256 { get; }
}
