using System.Security;
using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class PrimitiveGenerationPolicyLoader
{
    private readonly JsonSerializerOptions _serializerOptions;

    public PrimitiveGenerationPolicyLoader()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow
        };
    }

    public async Task<GenerationResult<PrimitiveGenerationPolicyDocument?>> LoadAsync(
        string policyPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyPath))
        {
            return Failure(
                policyPath ?? string.Empty,
                "A primitive generation policy path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(policyPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or
            PathTooLongException or SecurityException)
        {
            return Failure(
                policyPath,
                $"The primitive generation policy path is invalid: {exception.Message}");
        }

        if (!File.Exists(fullPath))
        {
            return Failure(
                fullPath,
                $"The primitive generation policy does not exist: '{fullPath}'.");
        }

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var document =
                await JsonSerializer.DeserializeAsync<PrimitiveGenerationPolicyDocument>(
                    stream,
                    _serializerOptions,
                    cancellationToken);

            return document is null
                ? Failure(fullPath, "The primitive generation policy contains no document.")
                : Success(document);
        }
        catch (JsonException exception)
        {
            return Failure(
                fullPath,
                $"The primitive generation policy is not valid JSON: {exception.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException or
            UnauthorizedAccessException or SecurityException)
        {
            return Failure(
                fullPath,
                $"The primitive generation policy could not be read: {exception.Message}");
        }
    }

    private static GenerationResult<PrimitiveGenerationPolicyDocument?> Success(
        PrimitiveGenerationPolicyDocument document)
    {
        return new GenerationResult<PrimitiveGenerationPolicyDocument?>(
            document,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static GenerationResult<PrimitiveGenerationPolicyDocument?> Failure(
        string sourceFile,
        string message)
    {
        return new GenerationResult<PrimitiveGenerationPolicyDocument?>(
            null,
            [new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.PrimitivePolicyReadFailure,
                GeneratorDiagnosticSeverity.Error,
                message,
                sourceFile)]);
    }
}
