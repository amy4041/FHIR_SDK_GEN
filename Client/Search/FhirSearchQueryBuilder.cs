namespace MyFhirSdk.Client.Search;

/// <summary>
/// Explicit builder wrapper for callers that prefer a separate builder object.
/// </summary>
public sealed class FhirSearchQueryBuilder
{
    private readonly FhirSearchQuery _query = FhirSearchQuery.Create();

    /// <summary>
    /// Adds a search parameter.
    /// </summary>
    public FhirSearchQueryBuilder Where(string name, string value)
    {
        _query.Where(name, value);
        return this;
    }

    /// <summary>
    /// Adds a _sort search parameter.
    /// </summary>
    public FhirSearchQueryBuilder Sort(string field)
    {
        _query.Sort(field);
        return this;
    }

    /// <summary>
    /// Adds a _count search parameter.
    /// </summary>
    public FhirSearchQueryBuilder Count(int count)
    {
        _query.Count(count);
        return this;
    }

    /// <summary>
    /// Builds the query.
    /// </summary>
    public FhirSearchQuery Build()
    {
        return _query;
    }
}
