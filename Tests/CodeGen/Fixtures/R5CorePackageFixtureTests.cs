using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Fixtures;

public sealed class R5CorePackageFixtureTests
{
    private const string ExpectedArchivePath =
        "Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz";

    [Fact]
    public void OfficialR5CorePackageBytesMatchApprovedLock()
    {
        using var lockDocument = ReadLockDocument();
        var lockRoot = lockDocument.RootElement;
        var archivePath = GetArchivePath();

        Assert.Equal(1, lockRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            ExpectedArchivePath,
            lockRoot.GetProperty("archivePath").GetString());
        Assert.Equal("package", lockRoot.GetProperty("archiveRoot").GetString());
        Assert.Equal("offline", lockRoot.GetProperty("ciInputPolicy").GetString());

        using var archive = File.OpenRead(archivePath);
        var actualHash = Convert
            .ToHexString(SHA256.HashData(archive))
            .ToLowerInvariant();

        Assert.Equal(lockRoot.GetProperty("sha256").GetString(), actualHash);
    }

    [Fact]
    public void OfficialR5CorePackageMetadataMatchesApprovedLock()
    {
        using var lockDocument = ReadLockDocument();
        using var packageDocument = ReadPackageDocument(GetArchivePath());
        var lockRoot = lockDocument.RootElement;
        var packageRoot = packageDocument.RootElement;

        Assert.Equal(
            lockRoot.GetProperty("packageId").GetString(),
            packageRoot.GetProperty("name").GetString());
        Assert.Equal(
            lockRoot.GetProperty("packageVersion").GetString(),
            packageRoot.GetProperty("version").GetString());
        Assert.Equal(
            lockRoot.GetProperty("packageType").GetString(),
            packageRoot.GetProperty("type").GetString());
        Assert.Equal(
            lockRoot.GetProperty("license").GetString(),
            packageRoot.GetProperty("license").GetString());
        Assert.Equal(
            lockRoot
                .GetProperty("fhirVersions")
                .EnumerateArray()
                .Select(version => version.GetString()),
            packageRoot
                .GetProperty("fhirVersions")
                .EnumerateArray()
                .Select(version => version.GetString()));
    }

    private static JsonDocument ReadLockDocument()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "r5-package-lock.json");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static JsonDocument ReadPackageDocument(string archivePath)
    {
        using var archive = File.OpenRead(archivePath);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (!string.Equals(
                    entry.Name.Replace('\\', '/'),
                    "package/package.json",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.DataStream is null)
            {
                throw new InvalidOperationException(
                    "The official R5 package has an empty package/package.json entry.");
            }

            return JsonDocument.Parse(entry.DataStream);
        }

        throw new InvalidOperationException(
            "The official R5 package does not contain package/package.json.");
    }

    private static string GetArchivePath()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                "The approved official R5 core package fixture was not copied.",
                path);
    }
}
