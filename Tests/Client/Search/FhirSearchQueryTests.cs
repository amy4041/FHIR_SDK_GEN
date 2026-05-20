namespace MyFhirSdk.Tests.Client.Search;

public static class FhirSearchQueryTests
{
    public static void ToQueryStringBuildsParametersInInsertionOrder()
    {
        var query = FhirSearchQuery.Create()
            .Where("name", "John Smith")
            .Sort("birthdate")
            .Count(20);

        var queryString = query.ToQueryString();

        TestAssert.AreEqual("name=John%20Smith&_sort=birthdate&_count=20", queryString);
    }

    public static void ToQueryStringReturnsEmptyStringForNoParameters()
    {
        var query = FhirSearchQuery.Create();

        TestAssert.AreEqual(string.Empty, query.ToQueryString());
    }

    public static void CountRejectsNegativeValues()
    {
        var query = FhirSearchQuery.Create();

        TestAssert.Throws<ArgumentOutOfRangeException>(() => query.Count(-1));
    }
}
