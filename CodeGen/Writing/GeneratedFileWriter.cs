using System.Security;
using System.Text;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Writing;

public sealed class GeneratedFileWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly string[] ProtectedSourceDirectories =
    [
        "core",
        "Types",
        "Resources",
        "Serialization",
        "Validation",
        "CodeGen",
        Path.Combine("Primitives", "Runtime")
    ];

    private readonly string _repositoryRoot;
    private readonly StringComparison _pathComparison;
    private readonly StringComparer _fileNameComparer;

    public GeneratedFileWriter(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        _repositoryRoot = NormalizeDirectoryPath(repositoryRoot);
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        _fileNameComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> WriteAsync(
        string outputRoot,
        IReadOnlyList<GeneratedSource> generatedSources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generatedSources);

        return await WriteArtifactsAsync(
            outputRoot,
            generatedSources
                .Select(source => source is null
                    ? null!
                    : new GeneratedArtifact(source.FileName, source.Source))
                .ToArray(),
            cancellationToken);
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> WriteArtifactsAsync(
        string outputRoot,
        IReadOnlyList<GeneratedArtifact> generatedArtifacts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generatedArtifacts);
        cancellationToken.ThrowIfCancellationRequested();

        var outputPathResult = ValidateOutputRoot(outputRoot);
        if (!outputPathResult.IsSuccess)
        {
            return Failure(outputPathResult.Diagnostic!);
        }

        var outputPath = outputPathResult.Path!;
        var sourceResult = PrepareArtifacts(generatedArtifacts, outputPath);
        if (!sourceResult.IsSuccess)
        {
            return Failure(sourceResult.Diagnostic!);
        }

        var sources = sourceResult.Sources!;
        var outputFiles = sources
            .Select(source => Path.Combine(outputPath, source.FileName))
            .ToArray();

        try
        {
            if (OutputMatches(outputPath, sources))
            {
                return Success(outputFiles);
            }

            var parentDirectory = Directory.GetParent(outputPath)?.FullName;
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                return Failure(CreateDiagnostic(
                    outputPath,
                    "The output root must have a parent directory."));
            }

            Directory.CreateDirectory(parentDirectory);

            var outputName = Path.GetFileName(outputPath);
            var transactionId = Guid.NewGuid().ToString("N");
            var stagingPath = Path.Combine(
                parentDirectory,
                $".{outputName}.staging-{transactionId}");
            var backupPath = Path.Combine(
                parentDirectory,
                $".{outputName}.backup-{transactionId}");

            return await CommitAsync(
                outputPath,
                stagingPath,
                backupPath,
                sources,
                outputFiles,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return Failure(CreateDiagnostic(
                outputPath,
                $"Could not write generated output: {exception.Message}"));
        }
    }

    private async Task<GenerationResult<IReadOnlyList<string>>> CommitAsync(
        string outputPath,
        string stagingPath,
        string backupPath,
        IReadOnlyList<PreparedSource> sources,
        IReadOnlyList<string> outputFiles,
        CancellationToken cancellationToken)
    {
        var existingOutputMoved = false;
        var newOutputInstalled = false;

        try
        {
            Directory.CreateDirectory(stagingPath);

            foreach (var source in sources)
            {
                var stagingFile = Path.Combine(stagingPath, source.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(stagingFile)!);
                await File.WriteAllBytesAsync(
                    stagingFile,
                    source.Bytes,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(outputPath))
            {
                Directory.Move(outputPath, backupPath);
                existingOutputMoved = true;
            }

            Directory.Move(stagingPath, outputPath);
            newOutputInstalled = true;

            if (existingOutputMoved)
            {
                TryDeleteDirectory(backupPath);
            }

            return Success(outputFiles);
        }
        catch (OperationCanceledException)
        {
            RestorePreviousOutput(
                outputPath,
                backupPath,
                existingOutputMoved,
                newOutputInstalled);
            throw;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            var rollbackFailure = RestorePreviousOutput(
                outputPath,
                backupPath,
                existingOutputMoved,
                newOutputInstalled);
            var rollbackMessage = rollbackFailure is null
                ? ""
                : $" Rollback also failed: {rollbackFailure.Message}";

            return Failure(CreateDiagnostic(
                outputPath,
                $"Could not commit generated output: {exception.Message}" +
                rollbackMessage));
        }
        finally
        {
            TryDeleteDirectory(stagingPath);
        }
    }

    private OutputPathValidation ValidateOutputRoot(string outputRoot)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return OutputPathValidation.Failure(CreateDiagnostic(
                outputRoot ?? "<output-root>",
                "The output root must be provided."));
        }

        if (ContainsParentTraversal(outputRoot))
        {
            return OutputPathValidation.Failure(CreateDiagnostic(
                outputRoot,
                "The output root must not contain '..' path traversal."));
        }

        string outputPath;
        try
        {
            outputPath = NormalizeDirectoryPath(outputRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or
            PathTooLongException or SecurityException)
        {
            return OutputPathValidation.Failure(CreateDiagnostic(
                outputRoot,
                $"The output root is invalid: {exception.Message}"));
        }

        if (PathsEqual(outputPath, _repositoryRoot))
        {
            return OutputPathValidation.Failure(CreateDiagnostic(
                outputPath,
                "The repository root cannot be used as the output root."));
        }

        foreach (var directoryName in ProtectedSourceDirectories)
        {
            var protectedPath = NormalizeDirectoryPath(
                Path.Combine(_repositoryRoot, directoryName));
            if (IsSameOrChildPath(outputPath, protectedPath))
            {
                return OutputPathValidation.Failure(CreateDiagnostic(
                    outputPath,
                    $"SDK source directory '{directoryName}' cannot be used " +
                    "as the output root."));
            }
        }

        if (File.Exists(outputPath))
        {
            return OutputPathValidation.Failure(CreateDiagnostic(
                outputPath,
                "The output root refers to an existing file."));
        }

        return OutputPathValidation.Success(outputPath);
    }

    private SourcePreparation PrepareArtifacts(
        IReadOnlyList<GeneratedArtifact> generatedArtifacts,
        string outputPath)
    {
        if (generatedArtifacts.Count == 0)
        {
            return SourcePreparation.Failure(CreateDiagnostic(
                outputPath,
                "The generated artifact batch must contain at least one file."));
        }

        var fileNames = new HashSet<string>(_fileNameComparer);
        var preparedSources = new List<PreparedSource>(generatedArtifacts.Count);

        foreach (var source in generatedArtifacts)
        {
            if (source is null)
            {
                return SourcePreparation.Failure(CreateDiagnostic(
                    outputPath,
                    "The generated artifact batch contains a null item."));
            }

            if (!TryNormalizeRelativePath(source.FileName, out var relativePath))
            {
                return SourcePreparation.Failure(CreateDiagnostic(
                    source.FileName ?? outputPath,
                    "Generated artifact paths must be safe relative paths " +
                    "without rooted paths or directory traversal."));
            }

            if (!fileNames.Add(relativePath))
            {
                return SourcePreparation.Failure(CreateDiagnostic(
                    source.FileName,
                    $"Generated artifact path '{source.FileName}' is " +
                    "duplicated."));
            }

            if (source.Content is null)
            {
                return SourcePreparation.Failure(CreateDiagnostic(
                    source.FileName,
                    $"Generated artifact '{source.FileName}' has no content."));
            }

            var content = NormalizeNewlines(source.Content);
            try
            {
                preparedSources.Add(new PreparedSource(
                    relativePath,
                    Utf8WithoutBom.GetBytes(content)));
            }
            catch (EncoderFallbackException exception)
            {
                return SourcePreparation.Failure(CreateDiagnostic(
                    source.FileName,
                    $"Generated artifact '{source.FileName}' is not valid " +
                    $"Unicode text: {exception.Message}"));
            }
        }

        return SourcePreparation.Success(preparedSources
            .OrderBy(source => source.FileName, StringComparer.Ordinal)
            .ToArray());
    }

    private static bool OutputMatches(
        string outputPath,
        IReadOnlyList<PreparedSource> sources)
    {
        if (!Directory.Exists(outputPath))
        {
            return false;
        }

        var existingFiles = Directory
            .EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                FullPath = path,
                RelativePath = Path.GetRelativePath(outputPath, path)
            })
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (existingFiles.Length != sources.Count)
        {
            return false;
        }

        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var existingFile = existingFiles[index];
            if (!string.Equals(
                    existingFile.RelativePath,
                    source.FileName,
                    StringComparison.Ordinal) ||
                !File.ReadAllBytes(existingFile.FullPath).AsSpan().SequenceEqual(source.Bytes))
            {
                return false;
            }
        }

        return true;
    }

    private static Exception? RestorePreviousOutput(
        string outputPath,
        string backupPath,
        bool existingOutputMoved,
        bool newOutputInstalled)
    {
        try
        {
            if (newOutputInstalled && Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }

            if (existingOutputMoved && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, outputPath);
            }

            return null;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return exception;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            // A committed output is valid even if transaction cleanup is delayed.
        }
    }

    private static bool IsFileSystemException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or
            SecurityException or NotSupportedException;
    }

    private static bool TryNormalizeRelativePath(
        string? artifactPath,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(artifactPath) || Path.IsPathRooted(artifactPath))
        {
            return false;
        }

        var segments = artifactPath.Split(['/', '\\'], StringSplitOptions.None);
        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0 ||
                segment.EndsWith(' ') || segment.EndsWith('.')))
        {
            return false;
        }

        relativePath = Path.Combine(segments);
        return true;
    }

    private static bool ContainsParentTraversal(string path)
    {
        return path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, _pathComparison);
    }

    private bool IsSameOrChildPath(string candidate, string parent)
    {
        return PathsEqual(candidate, parent) || candidate.StartsWith(
            parent + Path.DirectorySeparatorChar,
            _pathComparison);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static string NormalizeNewlines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        string sourceFile,
        string message)
    {
        return new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.UnsafeOutputPath,
            GeneratorDiagnosticSeverity.Error,
            message,
            sourceFile);
    }

    private static GenerationResult<IReadOnlyList<string>> Success(
        IReadOnlyList<string> outputFiles)
    {
        return new GenerationResult<IReadOnlyList<string>>(
            outputFiles,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static GenerationResult<IReadOnlyList<string>> Failure(
        GeneratorDiagnostic diagnostic)
    {
        return new GenerationResult<IReadOnlyList<string>>(
            Array.Empty<string>(),
            [diagnostic]);
    }

    private sealed record PreparedSource(
        string FileName,
        byte[] Bytes);

    private sealed record OutputPathValidation(
        string? Path,
        GeneratorDiagnostic? Diagnostic)
    {
        public bool IsSuccess => Diagnostic is null;

        public static OutputPathValidation Success(string path) =>
            new(path, null);

        public static OutputPathValidation Failure(
            GeneratorDiagnostic diagnostic) =>
            new(null, diagnostic);
    }

    private sealed record SourcePreparation(
        IReadOnlyList<PreparedSource>? Sources,
        GeneratorDiagnostic? Diagnostic)
    {
        public bool IsSuccess => Diagnostic is null;

        public static SourcePreparation Success(
            IReadOnlyList<PreparedSource> sources) =>
            new(sources, null);

        public static SourcePreparation Failure(
            GeneratorDiagnostic diagnostic) =>
            new(null, diagnostic);
    }
}
