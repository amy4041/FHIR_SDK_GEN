namespace MyFhirSdk.CodeGen.Models;

public sealed record CardinalityModel
{
    public CardinalityModel(
        int min,
        string max,
        bool isCollection,
        bool isRequired)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentException.ThrowIfNullOrWhiteSpace(max);

        Min = min;
        Max = max;
        IsCollection = isCollection;
        IsRequired = isRequired;
    }

    public int Min { get; }

    public string Max { get; }

    public bool IsCollection { get; }

    public bool IsRequired { get; }
}
