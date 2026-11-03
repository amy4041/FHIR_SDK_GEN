using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.Client;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using R4 = MyFhirSdk.R4.Resources;
using R5 = MyFhirSdk.Resources;

if (args.Length != 2 || args[0] is not ("R4" or "R5")) throw new ArgumentException("Usage: VersionedClientSmoke R4|R5 <summary.json>");
var release = args[0];
var output = Path.GetFullPath(args[1]);
var expected = release == "R4" ? "4.0.1" : "5.0.0";
var variable = $"MYFHIRSDK_{release}_INTEGRATION_BASE_URL";
var endpoint = Environment.GetEnvironmentVariable(variable)?.Trim();
var evidence = new Dictionary<string, object?> { ["release"] = release, ["expectedFhirVersion"] = expected, ["status"] = "Pending" };
var exit = 0;
try
{
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        evidence["status"] = "Skipped";
        evidence["reason"] = $"Set {variable} to run this release's external server smoke.";
    }
    else
    {
        var uri = new Uri(endpoint, UriKind.Absolute);
        if (uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Smoke endpoint must be an HTTP(S) FHIR base URL without userinfo, query or fragment.");
        evidence["endpoint"] = uri.AbsoluteUri;
        evidence["endpointSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var token = Environment.GetEnvironmentVariable($"MYFHIRSDK_{release}_INTEGRATION_BEARER_TOKEN");
        IAuthProvider auth = string.IsNullOrWhiteSpace(token) ? NoAuthProvider.Instance : new BearerTokenAuthProvider(token);
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(uri.AbsoluteUri.TrimEnd('/') + "/metadata"));
        metadataRequest.Headers.Accept.ParseAdd("application/fhir+json");
        await auth.ApplyAsync(metadataRequest);
        using var metadataHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var metadataResponse = await metadataHttp.SendAsync(metadataRequest);
        metadataResponse.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        var root = metadata.RootElement;
        evidence["serverFhirVersion"] = root.TryGetProperty("fhirVersion", out var fhirVersion) ? fhirVersion.GetString() : null;
        evidence["writesStarted"] = false;
        if (root.GetProperty("resourceType").GetString() != "CapabilityStatement" || root.GetProperty("fhirVersion").GetString() != expected)
            throw new InvalidOperationException("Server CapabilityStatement does not match the selected FHIR release; no resources were written.");
        evidence["serverFhirVersion"] = expected;
        evidence["serverSoftware"] = root.TryGetProperty("software", out var software)
            ? new { name = software.TryGetProperty("name", out var name) ? name.GetString() : null,
                    version = software.TryGetProperty("version", out var version) ? version.GetString() : null } : null;
        var options = new FhirClientOptions { BaseAddress = uri, ValidateBeforeSend = true };
        evidence["writesStarted"] = true;
        if (release == "R4")
            await Exercise<R4.Bundle, R4.OperationOutcome, R4.Patient>(http,
                new(R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation), options, auth,
                bundle => bundle.Entry.Select(e => e.Resource).OfType<Resource>());
        else await Exercise<R5.Bundle, R5.OperationOutcome, R5.Patient>(http, new MyFhirSdk.ModelMetadata.FhirModelContext(MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Metadata, MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Primitives, MyFhirSdk.ModelMetadata.R5.R5ModelProvider.Validation), options, auth,
            bundle => bundle.Entry.Select(e => e.Resource).OfType<Resource>());
        evidence["status"] = "Passed";
        evidence["operations"] = new[] { "metadata-version", "create", "read", "update", "search", "delete" };
    }
}
catch (Exception ex)
{
    exit = 1; evidence["status"] = "Failed";
    // Avoid persisting server bodies or credentials in evidence.
    evidence["failure"] = ex.GetType().FullName;
}
finally
{
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + "\n");
}
Console.WriteLine($"{release} external client smoke: {evidence["status"]}");
return exit;

async Task Exercise<TBundle, TOutcome, TPatient>(HttpClient http, FhirModelContext context, FhirClientOptions options,
    IAuthProvider auth, Func<TBundle, IEnumerable<Resource>> entries)
    where TBundle : Resource where TOutcome : Resource where TPatient : Resource
{
    const string system = "urn:myfhirsdk:v6-smoke";
    var marker = "v6-" + Guid.NewGuid().ToString("N");
    var client = new FhirClient<TBundle, TOutcome>(http, context, options, auth);
    var parser = new FhirJsonParser(context); var serializer = new FhirJsonSerializer(context);
    var patient = parser.Parse<TPatient>("{\"resourceType\":\"Patient\",\"active\":true,\"identifier\":[{\"system\":\"" + system + "\",\"value\":\"" + marker + "\"}]}");
    string? id = null; Exception? failure = null;
    try
    {
        var created = await client.CreateAsync(patient); id = created.Id;
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Create did not return a logical id.");
        Check(await client.ReadAsync<TPatient>(id));
        var updateJson = JsonNode.Parse(serializer.Serialize(created))!;
        updateJson["active"] = false;
        var updated = parser.Parse<TPatient>(updateJson.ToJsonString());
        var response = await client.UpdateAsync(updated); Check(response);
        using (var doc = JsonDocument.Parse(serializer.Serialize(response)))
            if (doc.RootElement.GetProperty("active").GetBoolean()) throw new InvalidOperationException("Update was not applied.");
        var found = false;
        for (var attempt = 0; attempt < 5 && !found; attempt++)
        {
            var bundle = await client.SearchAsync<TPatient>(FhirSearchQuery.Create().Where("identifier", system + "|" + marker));
            found = entries(bundle).Any(r => r is TPatient && r.Id == id);
            if (!found && attempt < 4) await Task.Delay(TimeSpan.FromSeconds(1));
        }
        if (!found) throw new InvalidOperationException("Created patient was absent from search results.");
    }
    catch (Exception ex) { failure = ex; throw; }
    finally
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { await client.DeleteAsync<TPatient>(id); }
            catch (Exception ex) when (failure is not null) { evidence["cleanupFailure"] = ex.GetType().FullName; }
        }
    }
    void Check(TPatient? value)
    {
        if (value is null || value.Id != id) throw new InvalidOperationException("Resource identity mismatch.");
        using var doc = JsonDocument.Parse(serializer.Serialize(value));
        if (!doc.RootElement.GetProperty("identifier").EnumerateArray().Any(i =>
                i.TryGetProperty("system", out var s) && s.GetString() == system && i.TryGetProperty("value", out var v) && v.GetString() == marker))
            throw new InvalidOperationException("Smoke marker was not preserved.");
    }
}
