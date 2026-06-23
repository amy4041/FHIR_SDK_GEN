namespace MyFhirSdk.Tests.Client.Search;

public sealed class FhirSearchQueryTests
{
    [Fact]
    public void ToQueryStringBuildsParametersInInsertionOrder()
    {
        var query = FhirSearchQuery.Create()
            .Where("name", "John Smith")
            .Sort("birthdate")
            .Count(20);

        var queryString = query.ToQueryString();

        Assert.Equal("name=John%20Smith&_sort=birthdate&_count=20", queryString);
    }

    [Fact]
    public void ToQueryStringReturnsEmptyStringForNoParameters()
    {
        var query = FhirSearchQuery.Create();

        Assert.Equal(string.Empty, query.ToQueryString());
    }

    [Fact]
    public void CountRejectsNegativeValues()
    {
        var query = FhirSearchQuery.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => query.Count(-1));
    }

    [Fact]
    public void ToQueryStringSupportsRepeatedParameterNames()
    {
        var query = FhirSearchQuery.Create()
            .Where("identifier", "a")
            .Where("identifier", "b");

        var queryString = query.ToQueryString();

        Assert.Equal("identifier=a&identifier=b", queryString);
    }

    [Fact]
    public void ToQueryStringPreservesTokenSearchValueFormat()
    {
        var query = FhirSearchQuery.Create()
            .Where("identifier", "http://hospital.example/mrn|12345");

        var queryString = query.ToQueryString();

        Assert.Equal("identifier=http%3A%2F%2Fhospital.example%2Fmrn%7C12345", queryString);
    }

    [Fact]
    public void ToQueryStringPreservesDateSearchValueFormat()
    {
        var query = FhirSearchQuery.Create()
            .Where("birthdate", "ge1990-01-01");

        var queryString = query.ToQueryString();

        Assert.Equal("birthdate=ge1990-01-01", queryString);
    }
}
