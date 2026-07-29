namespace MyFhirSdk.CodeGen.Models;

public sealed record FhirTypeModel
{
    public FhirTypeModel(
        string fhirName,
        string cSharpName,
        string @namespace,
        string cSharpBaseType,
        bool isAbstract,
        string sourceCanonical,
        string sourceVersion,
        IEnumerable<FhirPropertyModel> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cSharpName);
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(cSharpBaseType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCanonical);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        ArgumentNullException.ThrowIfNull(properties);

        FhirName = fhirName;
        CSharpName = cSharpName;
        Namespace = @namespace;
        CSharpBaseType = cSharpBaseType;
        IsAbstract = isAbstract;
        SourceCanonical = sourceCanonical;
        SourceVersion = sourceVersion;
        Properties = Array.AsReadOnly(properties.ToArray());
    }

    public string FhirName { get; }

    public string CSharpName { get; }

    public string Namespace { get; }

    public string CSharpBaseType { get; }

    public bool IsAbstract { get; }

    public string SourceCanonical { get; }

    public string SourceVersion { get; }

    public IReadOnlyList<FhirPropertyModel> Properties { get; }
}
