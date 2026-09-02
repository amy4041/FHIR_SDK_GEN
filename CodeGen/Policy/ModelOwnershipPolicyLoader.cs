using System.Security;
using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class ModelOwnershipPolicyLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public async Task<GenerationResult<ModelOwnershipPolicyDocument?>> LoadAsync(
        string policyPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyPath))
        {
            return Failure(policyPath ?? string.Empty, "A model ownership policy path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(policyPath);
        }
        catch (Exception exception) when (exception is ArgumentException or
            NotSupportedException or PathTooLongException or SecurityException)
        {
            return Failure(policyPath, $"The model ownership policy path is invalid: {exception.Message}");
        }

        if (!File.Exists(fullPath))
        {
            return Failure(fullPath, $"The model ownership policy does not exist: '{fullPath}'.");
        }

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var document = await JsonSerializer.DeserializeAsync<ModelOwnershipPolicyDocument>(
                stream,
                SerializerOptions,
                cancellationToken);
            return document is null
                ? Failure(fullPath, "The model ownership policy contains no document.")
                : new GenerationResult<ModelOwnershipPolicyDocument?>(
                    document,
                    Array.Empty<GeneratorDiagnostic>());
        }
        catch (JsonException exception)
        {
            return Failure(fullPath, $"The model ownership policy is not valid JSON: {exception.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
            NotSupportedException or UnauthorizedAccessException or SecurityException)
        {
            return Failure(fullPath, $"The model ownership policy could not be read: {exception.Message}");
        }
    }

    private static GenerationResult<ModelOwnershipPolicyDocument?> Failure(
        string sourceFile,
        string message) =>
        new(
            null,
            [new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.ModelOwnershipPolicyReadFailure,
                GeneratorDiagnosticSeverity.Error,
                message,
                sourceFile)]);
}
