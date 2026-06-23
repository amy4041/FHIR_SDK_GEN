namespace MyFhirSdk.Tests.Client.Integration;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public const string BaseUrlEnvironmentVariableName = "MYFHIRSDK_INTEGRATION_BASE_URL";
    public const string BearerTokenEnvironmentVariableName = "MYFHIRSDK_INTEGRATION_BEARER_TOKEN";

    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName)))
        {
            Skip = $"Set {BaseUrlEnvironmentVariableName} to run integration smoke tests.";
        }
    }
}
