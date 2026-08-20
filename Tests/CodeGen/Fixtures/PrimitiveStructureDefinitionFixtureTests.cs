using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Fixtures;

public sealed class PrimitiveStructureDefinitionFixtureTests
{
    private static readonly string[] ExpectedPrimitiveTypeNames =
    [
        "base64Binary",
        "boolean",
        "canonical",
        "code",
        "date",
        "dateTime",
        "decimal",
        "id",
        "instant",
        "integer",
        "integer64",
        "markdown",
        "oid",
        "positiveInt",
        "string",
        "time",
        "unsignedInt",
        "uri",
        "url",
        "uuid",
        "xhtml"
    ];

    [Fact]
    public void OfficialR5PrimitiveFixtureInventoryIsCompleteAndUnique()
    {
        var fixtureDirectory = GetFixtureDirectory();
        var definitions = Directory
            .EnumerateFiles(
                fixtureDirectory,
                "StructureDefinition-*.json",
                SearchOption.TopDirectoryOnly)
            .Select(ReadIdentity)
            .OrderBy(identity => identity.Type, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedPrimitiveTypeNames.Length, definitions.Length);
        Assert.Equal(
            ExpectedPrimitiveTypeNames,
            definitions.Select(identity => identity.Type));
        Assert.Equal(
            definitions.Length,
            definitions
                .Select(identity => identity.Canonical)
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.All(
            definitions,
            identity =>
            {
                Assert.Equal("StructureDefinition", identity.ResourceType);
                Assert.Equal("primitive-type", identity.Kind);
                Assert.Equal("5.0.0", identity.Version);
                Assert.Equal(
                    $"http://hl7.org/fhir/StructureDefinition/{identity.Type}",
                    identity.Canonical);
                Assert.Equal(
                    $"StructureDefinition-{identity.Type}.json",
                    identity.FileName);
            });
    }

    [Fact]
    public void OfficialR5PrimitiveFixtureBytesMatchApprovedChecksums()
    {
        var fixtureDirectory = GetFixtureDirectory();
        var checksumPath = Path.Combine(fixtureDirectory, "SHA256SUMS.txt");
        var expectedChecksums = File
            .ReadAllLines(checksumPath)
            .Select(ParseChecksum)
            .ToDictionary(
                entry => entry.FileName,
                entry => entry.Hash,
                StringComparer.Ordinal);
        var fixtureFiles = Directory
            .EnumerateFiles(
                fixtureDirectory,
                "StructureDefinition-*.json",
                SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(fixtureFiles.Length, expectedChecksums.Count);
        foreach (var fixtureFile in fixtureFiles)
        {
            var fileName = Path.GetFileName(fixtureFile);
            Assert.True(
                expectedChecksums.TryGetValue(fileName, out var expectedHash),
                $"Missing checksum for '{fileName}'.");

            var actualHash = Convert
                .ToHexString(SHA256.HashData(File.ReadAllBytes(fixtureFile)))
                .ToLowerInvariant();
            Assert.Equal(expectedHash, actualHash);
        }
    }

    private static FixtureIdentity ReadIdentity(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        return new FixtureIdentity(
            Path.GetFileName(path),
            GetRequiredString(root, "resourceType", path),
            GetRequiredString(root, "kind", path),
            GetRequiredString(root, "version", path),
            GetRequiredString(root, "type", path),
            GetRequiredString(root, "url", path));
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName,
        string sourceFile)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException(
                $"Fixture '{sourceFile}' has no non-empty '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static ChecksumEntry ParseChecksum(string line)
    {
        const int Sha256Length = 64;
        if (line.Length <= Sha256Length + 2 ||
            line[Sha256Length] != ' ' ||
            line[Sha256Length + 1] != ' ')
        {
            throw new InvalidOperationException(
                $"Invalid SHA256SUMS entry: '{line}'.");
        }

        var hash = line[..Sha256Length];
        var fileName = line[(Sha256Length + 2)..];
        if (hash.Any(character => !Uri.IsHexDigit(character)) ||
            string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException(
                $"Invalid SHA256SUMS entry: '{line}'.");
        }

        return new ChecksumEntry(fileName, hash);
    }

    private static string GetFixtureDirectory()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Primitives",
            "R5");

        return Directory.Exists(path)
            ? path
            : throw new DirectoryNotFoundException(
                $"Primitive fixture directory was not copied to '{path}'.");
    }

    private sealed record FixtureIdentity(
        string FileName,
        string ResourceType,
        string Kind,
        string Version,
        string Type,
        string Canonical);

    private sealed record ChecksumEntry(string FileName, string Hash);
}
