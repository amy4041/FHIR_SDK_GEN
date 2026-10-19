using System.Net;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using Xunit;
using R4R = MyFhirSdk.R4.Resources;
using R5R = MyFhirSdk.Resources;

namespace MyFhirSdk.Tests.FhirVersions.V6;

public sealed class ClientCompositionTests
{
    private static FhirModelContext R4Context() => new(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
    private static FhirClientOptions Options => new() { BaseAddress = new("https://example.test/fhir") };

    [Fact]
    public void MixedReleaseAndIncorrectBundleOrOutcomeArgumentsAreRejectedAtConstruction()
    {
        using var http = new HttpClient(); var context = R4Context();
        Assert.Throws<FhirSdkException>(() => new FhirClient<R5R.Bundle, R4R.OperationOutcome>(http, context, Options));
        Assert.Throws<FhirSdkException>(() => new FhirClient<R4R.Bundle, R5R.OperationOutcome>(http, context, Options));
        Assert.Throws<ArgumentException>(() => new FhirClient<R4R.Patient, R4R.OperationOutcome>(http, context, Options));
        Assert.Throws<ArgumentException>(() => new FhirClient<R4R.Bundle, R4R.Patient>(http, context, Options));
        Assert.Throws<ArgumentNullException>(() => new FhirClient<R4R.Bundle, R4R.OperationOutcome>(http, null!, Options));
        Assert.Throws<ArgumentNullException>(() => new FhirClient<R4R.Bundle, R4R.OperationOutcome>(null!, context, Options));
        Assert.Throws<ArgumentNullException>(() => new FhirClient<R4R.Bundle, R4R.OperationOutcome>(http, context, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FhirClient<R4R.Bundle, R4R.OperationOutcome>(http, context,
            new() { BaseAddress = Options.BaseAddress, Timeout = TimeSpan.Zero }));
    }

    [Fact]
    public async Task LegacyClientKeepsR5DefaultsAndRejectsForeignReadsBeforeHttp()
    {
        using var handler = new ClientContractTests<R5R.Bundle, R5R.OperationOutcome, R5R.Patient>.QueueHandler();
        using var http = new HttpClient(handler);
        handler.Enqueue(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"p\"}");
        var client = new FhirClient(http, new FhirJsonSerializer(), new FhirJsonParser(), Options);
        Assert.IsType<R5R.Patient>(await client.ReadAsync<R5R.Patient>("p"));
        await Assert.ThrowsAsync<FhirSdkException>(() => client.ReadAsync<R4R.Patient>("p"));
        Assert.Single(handler.Requests);
        Assert.Equal(2, typeof(FhirClient).GetConstructors().Length);
        Assert.All(typeof(FhirClient).GetMethods().Where(m => m.Name == "SearchAsync"),
            m => Assert.Equal(typeof(Task<R5R.Bundle>), m.ReturnType));
    }

    [Fact]
    public async Task ParallelR4AndR5ClientsUseIndependentFactoriesAndWireRules()
    {
        using var h4 = new ClientContractTests<R4R.Bundle, R4R.OperationOutcome, R4R.Patient>.QueueHandler();
        using var h5 = new ClientContractTests<R5R.Bundle, R5R.OperationOutcome, R5R.Patient>.QueueHandler();
        using var http4 = new HttpClient(h4); using var http5 = new HttpClient(h5);
        var started4 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started5 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h4.OnSend = async (_, ct) =>
        {
            started4.TrySetResult();
            await started5.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"resourceType":"Patient","photo":[{"size":42}]}""") };
        };
        h5.OnSend = async (_, ct) =>
        {
            started5.TrySetResult();
            await started4.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"resourceType":"Patient","photo":[{"size":"42"}]}""") };
        };
        var c4 = new FhirClient<R4R.Bundle, R4R.OperationOutcome>(http4, R4Context(), Options);
        var c5 = new FhirClient<R5R.Bundle, R5R.OperationOutcome>(http5, R5ModelContext.Default, Options);
        await Task.WhenAll(Enumerable.Range(0, 24).Select(async attempt =>
        {
            var read4 = c4.ReadAsync<R4R.Patient>("p");
            if (attempt == 0) Assert.False(read4.IsCompleted);
            var read5 = c5.ReadAsync<R5R.Patient>("p");
            await Task.WhenAll(read4, read5);
            var r4 = await read4; var r5 = await read5;
            Assert.Equal(42, r4!.Photo.Single().Size!.Value); Assert.Equal(42L, r5!.Photo.Single().Size!.Value);
            Assert.NotSame(r4.GetType().Assembly, r5.GetType().Assembly);
        }));
        Assert.Equal(24, h4.Requests.Count); Assert.Equal(24, h5.Requests.Count);
    }

    [Fact]
    public void ClientApiMatchesApprovedSnapshot()
    {
        var actual = MyFhirSdk.Tests.Architecture.R5ModelPublicApiSnapshot.Create(typeof(FhirClient).Assembly.GetExportedTypes()
            .Where(t => t.Namespace is not null && (t.Namespace == "MyFhirSdk.Client" || t.Namespace.StartsWith("MyFhirSdk.Client.", StringComparison.Ordinal))));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ActualClientApi.txt"), actual + "\n");
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ApprovedClientApi.txt")).Replace("\r\n", "\n").TrimEnd(), actual);
    }
}
