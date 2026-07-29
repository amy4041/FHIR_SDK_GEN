namespace MyFhirSdk.CodeGen.Models;

public sealed record FhirPropertyModel
{
    public FhirPropertyModel(
        string elementId,
        string elementPath,
        string fhirName,
        string cSharpName,
        string cSharpType,
        CardinalityModel cardinality,
        string? documentation,
        int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cSharpName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cSharpType);
        ArgumentNullException.ThrowIfNull(cardinality);
        ArgumentOutOfRangeException.ThrowIfNegative(order);

        ElementId = elementId;
        ElementPath = elementPath;
        FhirName = fhirName;
        CSharpName = cSharpName;
        CSharpType = cSharpType;
        Cardinality = cardinality;
        Documentation = documentation;
        Order = order;
    }

    public string ElementId { get; }

    public string ElementPath { get; }

    public string FhirName { get; }

    public string CSharpName { get; }

    public string CSharpType { get; }

    public CardinalityModel Cardinality { get; }

    public bool IsCollection => Cardinality.IsCollection;

    public bool IsRequired => Cardinality.IsRequired;

    public int Min => Cardinality.Min;

    public string Max => Cardinality.Max;

    public string? Documentation { get; }

    public int Order { get; }
}
