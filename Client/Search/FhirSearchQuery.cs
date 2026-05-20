using System.Collections.ObjectModel;

namespace MyFhirSdk.Client.Search;

/// <summary>
/// Mutable builder-style representation of FHIR search query parameters.
/// </summary>
public sealed class FhirSearchQuery
{
    private readonly List<FhirSearchParameter> _parameters = new();

    private FhirSearchQuery()
    {
    }

    /// <summary>
    /// Query parameters in insertion order.
    /// </summary>
    public IReadOnlyList<FhirSearchParameter> Parameters => new ReadOnlyCollection<FhirSearchParameter>(_parameters);

    /// <summary>
    /// Creates a new search query.
    /// </summary>
    public static FhirSearchQuery Create()
    {
        return new FhirSearchQuery();
    }

    /// <summary>
    /// Adds a search parameter.
    /// </summary>
    public FhirSearchQuery Where(string name, string value)
    {
        return Add(name, value);
    }

    /// <summary>
    /// Adds a _sort search parameter.
    /// </summary>
    public FhirSearchQuery Sort(string field)
    {
        return Add("_sort", field);
    }

    /// <summary>
    /// Adds a _count search parameter.
    /// </summary>
    public FhirSearchQuery Count(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "FHIR search count cannot be negative.");
        }

        return Add("_count", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Adds a raw named parameter.
    /// </summary>
    public FhirSearchQuery Add(string name, string value)
    {
        _parameters.Add(new FhirSearchParameter(name, value));
        return this;
    }

    /// <summary>
    /// Converts this query into an encoded query string without a leading question mark.
    /// </summary>
    public string ToQueryString()
    {
        return string.Join("&", _parameters.Select(parameter => parameter.ToQueryString()));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToQueryString();
    }
}
