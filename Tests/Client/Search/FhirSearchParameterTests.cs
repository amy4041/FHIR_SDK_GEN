namespace MyFhirSdk.Tests.Client.Search;

public static class FhirSearchParameterTests
{
    public static void ToQueryStringEncodesNameAndValue()
    {
        var parameter = new FhirSearchParameter("name", "John Smith");

        var queryString = parameter.ToQueryString();

        TestAssert.AreEqual("name=John%20Smith", queryString);
    }

    public static void ConstructorRejectsEmptyName()
    {
        TestAssert.Throws<ArgumentException>(() => new FhirSearchParameter(" ", "John"));
    }
}
