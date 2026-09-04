using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Tests.Client.Integration;
using MyFhirSdk.Types;

namespace MyFhirSdk.Tests.Client;

public sealed class FhirClientIntegrationSmokeTests
{
    private const string TestIdentifierSystem = "urn:myfhirsdk:integration-smoke";

    [IntegrationFact]
    [Trait("Category", "Integration")]
    public async global::System.Threading.Tasks.Task PatientCrudSearchSmokeFlow()
    {
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient);
        var marker = "smoke-" + Guid.NewGuid().ToString("N");
        Console.WriteLine($"marker: {marker}");
        var patient = CreateSmokeTestPatient(marker);

        var created = await client.CreateAsync(patient);

        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        AssertSmokeTestIdentifier(created, marker);

        var read = await client.ReadAsync<Patient>(created.Id);

        Assert.NotNull(read);
        Assert.Equal(created.Id, read.Id);
        AssertSmokeTestIdentifier(read, marker);

        var searched = await WaitForPatientSearchResultAsync(client, marker, created.Id);

        Assert.NotNull(searched);
        Assert.Equal(created.Id, searched.Id);

        created.Active = new FhirBoolean(false);
        var updated = await client.UpdateAsync(created);

        Assert.Equal(created.Id, updated.Id);
        Assert.False(updated.Active?.Value);

        var updatedRead = await client.ReadAsync<Patient>(created.Id);

        Assert.NotNull(updatedRead);
        Assert.False(updatedRead.Active?.Value);
    }

    private static FhirClient CreateClient(HttpClient httpClient)
    {
        var baseUrl = Environment.GetEnvironmentVariable(
            IntegrationFactAttribute.BaseUrlEnvironmentVariableName);

        return new FhirClient(
            httpClient,
            new FhirJsonSerializer(),
            new FhirJsonParser(),
            new FhirClientOptions
            {
                BaseAddress = new Uri(baseUrl!, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(30),
                ValidateBeforeSend = true
            },
            authProvider: CreateAuthProvider());
    }

    private static IAuthProvider? CreateAuthProvider()
    {
        var token = Environment.GetEnvironmentVariable(
            IntegrationFactAttribute.BearerTokenEnvironmentVariableName);

        return string.IsNullOrWhiteSpace(token)
            ? null
            : new BearerTokenAuthProvider(token);
    }

    private static Patient CreateSmokeTestPatient(string marker)
    {
        var patient = new Patient
        {
            Active = new FhirBoolean(true)
        };

        patient.Identifier.Add(new Identifier
        {
            System = new FhirUri(TestIdentifierSystem),
            Value = new FhirString(marker)
        });

        patient.Name.Add(new HumanName
        {
            Family = new FhirString("MyFhirSdkSmoke"),
            Given = { new FhirString(marker) }
        });

        return patient;
    }

    private static async global::System.Threading.Tasks.Task<Patient?> WaitForPatientSearchResultAsync(
        FhirClient client,
        string marker,
        string expectedId)
    {
        var query = FhirSearchQuery.Create()
            .Where("identifier", TestIdentifierSystem + "|" + marker);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var bundle = await client.SearchAsync<Patient>(query);
            var patient = bundle.Entry
                .Select(entry => entry.Resource)
                .OfType<Patient>()
                .FirstOrDefault(candidate => string.Equals(candidate.Id, expectedId, StringComparison.Ordinal));

            if (patient is not null)
            {
                return patient;
            }

            if (attempt < 5)
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        return null;
    }

    private static void AssertSmokeTestIdentifier(Patient patient, string expectedMarker)
    {
        Assert.Contains(
            patient.Identifier,
            identifier =>
                string.Equals(identifier.System?.Value, TestIdentifierSystem, StringComparison.Ordinal) &&
                string.Equals(identifier.Value?.Value, expectedMarker, StringComparison.Ordinal));
    }
}
