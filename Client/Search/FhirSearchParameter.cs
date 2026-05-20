namespace MyFhirSdk.Client.Search;

/// <summary>
/// A single FHIR search query parameter.
/// </summary>
public sealed class FhirSearchParameter
{
    /// <summary>
    /// Creates a search parameter.
    /// </summary>
    public FhirSearchParameter(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("FHIR search parameter name cannot be empty.", nameof(name));
        }

        Name = name;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Parameter value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Converts this parameter into an encoded name=value pair.
    /// </summary>
    public string ToQueryString()
    {
        return $"{Uri.EscapeDataString(Name)}={Uri.EscapeDataString(Value)}";
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToQueryString();
    }
}
