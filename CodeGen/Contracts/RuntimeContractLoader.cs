using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Contracts;

public sealed class RuntimeContractLoader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly RuntimeContractValidator _validator;

    public RuntimeContractLoader(RuntimeContractValidator? validator = null)
    {
        _validator = validator ?? new RuntimeContractValidator();
    }

    public async Task<GenerationResult<RuntimeContractView?>> LoadAsync(
        string descriptorPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(descriptorPath))
        {
            return Failure(
                GeneratorDiagnosticCodes.RuntimeContractReadFailure,
                descriptorPath ?? string.Empty,
                "A Runtime contract descriptor path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(descriptorPath);
        }
        catch (Exception exception) when (exception is ArgumentException or
            NotSupportedException or PathTooLongException or SecurityException)
        {
            return Failure(
                GeneratorDiagnosticCodes.RuntimeContractReadFailure,
                descriptorPath,
                $"The Runtime contract descriptor path is invalid: {exception.Message}");
        }

        if (!File.Exists(fullPath))
        {
            return Failure(
                GeneratorDiagnosticCodes.RuntimeContractReadFailure,
                fullPath,
                $"The Runtime contract descriptor does not exist: '{fullPath}'.");
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
            NotSupportedException or UnauthorizedAccessException or SecurityException)
        {
            return Failure(
                GeneratorDiagnosticCodes.RuntimeContractReadFailure,
                fullPath,
                $"The Runtime contract descriptor could not be read: {exception.Message}");
        }

        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContractJson,
                fullPath,
                "The Runtime contract descriptor is not valid UTF-8.");
        }

        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContract,
                fullPath,
                "The Runtime contract descriptor must be UTF-8 without a byte-order mark.");
        }
        if (json.Contains('\r', StringComparison.Ordinal))
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContract,
                fullPath,
                "The Runtime contract descriptor must use LF newlines.");
        }

        JsonDocument jsonDocument;
        try
        {
            jsonDocument = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException)
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContractJson,
                fullPath,
                "The Runtime contract descriptor is not valid JSON.");
        }

        using (jsonDocument)
        {
            var duplicatePaths = FindDuplicateProperties(jsonDocument.RootElement);
            if (duplicatePaths.Count > 0)
            {
                return new GenerationResult<RuntimeContractView?>(
                    null,
                    duplicatePaths.Select(path => new GeneratorDiagnostic(
                        GeneratorDiagnosticCodes.DuplicateRuntimeContractEntry,
                        GeneratorDiagnosticSeverity.Error,
                        $"The Runtime contract descriptor contains duplicate JSON property '{path}'.",
                        fullPath)).ToArray());
            }
        }

        RuntimeContractDescriptorDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<RuntimeContractDescriptorDocument>(
                bytes,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContract,
                fullPath,
                "The Runtime contract descriptor does not match schema v1.");
        }

        if (document is null)
        {
            return Failure(
                GeneratorDiagnosticCodes.InvalidRuntimeContract,
                fullPath,
                "The Runtime contract descriptor contains no document.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return _validator.Validate(document, hash, fullPath);
    }

    private static IReadOnlyList<string> FindDuplicateProperties(JsonElement root)
    {
        var duplicates = new SortedSet<string>(StringComparer.Ordinal);
        Visit(root, "$", duplicates);
        return duplicates.ToArray();
    }

    private static void Visit(
        JsonElement element,
        string path,
        ISet<string> duplicates)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    duplicates.Add(propertyPath);
                }
                Visit(property.Value, propertyPath, duplicates);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, $"{path}[{index++}]", duplicates);
            }
        }
    }

    private static GenerationResult<RuntimeContractView?> Failure(
        string code,
        string sourceFile,
        string message) =>
        new(
            null,
            [new GeneratorDiagnostic(
                code,
                GeneratorDiagnosticSeverity.Error,
                message,
                sourceFile)]);
}
