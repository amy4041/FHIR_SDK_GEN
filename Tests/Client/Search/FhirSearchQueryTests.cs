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
}
