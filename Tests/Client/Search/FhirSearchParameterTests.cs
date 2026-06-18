namespace MyFhirSdk.Tests.Client.Search;

public sealed class FhirSearchParameterTests
{
    [Fact]
    public void ToQueryStringEncodesNameAndValue()
    {
        var parameter = new FhirSearchParameter("name", "John Smith");

        var queryString = parameter.ToQueryString();

        Assert.Equal("name=John%20Smith", queryString);
    }

    [Fact]
    public void ConstructorRejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new FhirSearchParameter(" ", "John"));
    }
}
