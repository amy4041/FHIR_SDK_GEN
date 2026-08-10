using System.Text;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Writing;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Writing;

public sealed class GeneratedFileWriterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _repositoryRoot;

    public GeneratedFileWriterTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "MyFhirSdk-CodeGen-WriterTests",
            Guid.NewGuid().ToString("N"));
        _repositoryRoot = Path.Combine(_testRoot, "repository");
        Directory.CreateDirectory(_repositoryRoot);
    }

    [Fact]
    public async Task WriteAsync_ValidBatch_ReplacesOutputWithDeterministicUtf8LfFiles()
    {
        var outputRoot = Path.Combine(_testRoot, "Generated", "R5", "Types");
        Directory.CreateDirectory(outputRoot);
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, "Stale.g.cs"),
            "stale");
        var sources = new[]
        {
            Source("Zeta.g.cs", "line1\rline2\r\n"),
            Source("Alpha.g.cs", "// α\r\nclass Alpha { }\r\n")
        };

        var result = await CreateWriter().WriteAsync(outputRoot, sources);

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            ["Alpha.g.cs", "Zeta.g.cs"],
            result.Value.Select(Path.GetFileName));
        Assert.Equal(
            ["Alpha.g.cs", "Zeta.g.cs"],
            Directory.EnumerateFiles(outputRoot)
                .Select(Path.GetFileName)
                .OrderBy(fileName => fileName, StringComparer.Ordinal));

        var alphaPath = Path.Combine(outputRoot, "Alpha.g.cs");
        var alphaBytes = await File.ReadAllBytesAsync(alphaPath);
        Assert.False(alphaBytes.AsSpan().StartsWith(
            new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal("// α\nclass Alpha { }\n", Encoding.UTF8.GetString(alphaBytes));
        Assert.Equal(
            "line1\nline2\n",
            await File.ReadAllTextAsync(Path.Combine(outputRoot, "Zeta.g.cs")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "Stale.g.cs")));
        Assert.Empty(FindTransactionDirectories(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_UnchangedBatch_DoesNotRewriteOutput()
    {
        var outputRoot = Path.Combine(_testRoot, "preview-output");
        var sources = new[] { Source("HumanName.g.cs", "class HumanName { }\n") };
        var writer = CreateWriter();
        var firstResult = await writer.WriteAsync(outputRoot, sources);
        Assert.True(firstResult.IsSuccess, FormatDiagnostics(firstResult.Diagnostics));

        var outputFile = Path.Combine(outputRoot, "HumanName.g.cs");
        var preservedTimestamp = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(outputFile, preservedTimestamp);
        var timestampBeforeSecondWrite = File.GetLastWriteTimeUtc(outputFile);

        var secondResult = await writer.WriteAsync(outputRoot, sources);

        Assert.True(secondResult.IsSuccess, FormatDiagnostics(secondResult.Diagnostics));
        Assert.Equal(timestampBeforeSecondWrite, File.GetLastWriteTimeUtc(outputFile));
        Assert.Empty(FindTransactionDirectories(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_RepositoryRoot_ReturnsFsg0011WithoutModification()
    {
        var markerPath = Path.Combine(_repositoryRoot, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "keep");

        var result = await CreateWriter().WriteAsync(
            _repositoryRoot,
            [Source("HumanName.g.cs", "class HumanName { }")]);

        AssertUnsafeOutput(result, _repositoryRoot);
        Assert.Equal("keep", await File.ReadAllTextAsync(markerPath));
    }

    [Theory]
    [InlineData("core")]
    [InlineData("Types")]
    [InlineData("Resources")]
    [InlineData("Serialization")]
    [InlineData("Validation")]
    public async Task WriteAsync_SdkSourceDirectory_ReturnsFsg0011(
        string directoryName)
    {
        var outputRoot = Path.Combine(_repositoryRoot, directoryName);

        var result = await CreateWriter().WriteAsync(
            outputRoot,
            [Source("HumanName.g.cs", "class HumanName { }")]);

        AssertUnsafeOutput(result, Path.GetFullPath(outputRoot));
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_ParentTraversal_ReturnsFsg0011()
    {
        var outputRoot = Path.Combine(
            _repositoryRoot,
            "Generated",
            "..",
            "Types");

        var result = await CreateWriter().WriteAsync(
            outputRoot,
            [Source("HumanName.g.cs", "class HumanName { }")]);

        var diagnostic = AssertUnsafeOutput(result, outputRoot);
        Assert.Contains("path traversal", diagnostic.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_repositoryRoot, "Types")));
    }

    [Fact]
    public async Task WriteAsync_UnsafeGeneratedFileName_LeavesExistingBatchUntouched()
    {
        var outputRoot = Path.Combine(_testRoot, "preview-output");
        Directory.CreateDirectory(outputRoot);
        var existingFile = Path.Combine(outputRoot, "Existing.g.cs");
        await File.WriteAllTextAsync(existingFile, "original");
        var sources = new[]
        {
            Source("NewType.g.cs", "class NewType { }"),
            Source("..\\Types\\HumanName.cs", "class HumanName { }")
        };

        var result = await CreateWriter().WriteAsync(outputRoot, sources);

        AssertUnsafeOutput(result, "..\\Types\\HumanName.cs");
        Assert.Equal("original", await File.ReadAllTextAsync(existingFile));
        Assert.False(File.Exists(Path.Combine(outputRoot, "NewType.g.cs")));
        Assert.Empty(FindTransactionDirectories(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_EmptyBatch_DoesNotDeleteExistingOutput()
    {
        var outputRoot = Path.Combine(_testRoot, "preview-output");
        Directory.CreateDirectory(outputRoot);
        var existingFile = Path.Combine(outputRoot, "Existing.g.cs");
        await File.WriteAllTextAsync(existingFile, "original");

        var result = await CreateWriter().WriteAsync(
            outputRoot,
            Array.Empty<GeneratedSource>());

        AssertUnsafeOutput(result, Path.GetFullPath(outputRoot));
        Assert.Equal("original", await File.ReadAllTextAsync(existingFile));
        Assert.Empty(FindTransactionDirectories(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_InvalidUnicode_LeavesExistingBatchUntouched()
    {
        var outputRoot = Path.Combine(_testRoot, "preview-output");
        Directory.CreateDirectory(outputRoot);
        var existingFile = Path.Combine(outputRoot, "Existing.g.cs");
        await File.WriteAllTextAsync(existingFile, "original");

        var result = await CreateWriter().WriteAsync(
            outputRoot,
            [
                Source("Good.g.cs", "class Good { }"),
                Source("Bad.g.cs", "\uD800")
            ]);

        AssertUnsafeOutput(result, "Bad.g.cs");
        Assert.Equal("original", await File.ReadAllTextAsync(existingFile));
        Assert.False(File.Exists(Path.Combine(outputRoot, "Good.g.cs")));
        Assert.Empty(FindTransactionDirectories(outputRoot));
    }

    [Fact]
    public async Task WriteAsync_FileSystemFailure_ReturnsLocatableDiagnostic()
    {
        var blockedParent = Path.Combine(_testRoot, "blocked");
        await File.WriteAllTextAsync(blockedParent, "not a directory");
        var outputRoot = Path.Combine(blockedParent, "Generated");

        var result = await CreateWriter().WriteAsync(
            outputRoot,
            [Source("HumanName.g.cs", "class HumanName { }")]);

        var diagnostic = AssertUnsafeOutput(result, Path.GetFullPath(outputRoot));
        Assert.Contains(
            "Could not write generated output",
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.True(File.Exists(blockedParent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private GeneratedFileWriter CreateWriter()
    {
        return new GeneratedFileWriter(_repositoryRoot);
    }

    private static GeneratedSource Source(string fileName, string content)
    {
        return new GeneratedSource(fileName, content);
    }

    private static GeneratorDiagnostic AssertUnsafeOutput(
        GenerationResult<IReadOnlyList<string>> result,
        string expectedSourceFile)
    {
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.UnsafeOutputPath, diagnostic.Code);
        Assert.Equal(GeneratorDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(expectedSourceFile, diagnostic.SourceFile);
        return diagnostic;
    }

    private static string[] FindTransactionDirectories(string outputRoot)
    {
        var parent = Directory.GetParent(outputRoot)?.FullName;
        if (parent is null || !Directory.Exists(parent))
        {
            return [];
        }

        var outputName = Path.GetFileName(outputRoot);
        return Directory
            .EnumerateDirectories(parent, $".{outputName}.*-*")
            .ToArray();
    }

    private static string FormatDiagnostics(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"[{diagnostic.Code}] {diagnostic.SourceFile}: " +
                diagnostic.Message));
    }
}
