namespace MyFhirSdk.ModelContracts;

/// <summary>Identity of one compatible release, policy and CLR model composition.</summary>
public sealed record ModelIdentity
{
    public ModelIdentity(string fhirVersion, string packageId, string packageVersion,
        string policyId, string modelId)
    {
        foreach (var value in new[] { fhirVersion, packageId, packageVersion, policyId, modelId })
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
        FhirVersion = fhirVersion;
        PackageId = packageId;
        PackageVersion = packageVersion;
        PolicyId = policyId;
        ModelId = modelId;
    }

    public string FhirVersion { get; }
    public string PackageId { get; }
    public string PackageVersion { get; }
    public string PolicyId { get; }
    public string ModelId { get; }
}

/// <summary>Explicitly advertised support; absence does not imply complete FHIR support.</summary>
public sealed record ModelCapabilities(bool CompleteResourceSet, bool PrimitiveValidation,
    bool RequiredAndChoiceValidation, bool InvariantValidation, bool ProfileValidation);
