using MyFhirSdk.Core;

namespace MyFhirSdk.Tests.Architecture;

public sealed class R5ModelPublicApiSnapshotTests
{
    [Fact]
    public void R5ModelPublicApiMatchesApprovedBaseline()
    {
        var approvedPath = Path.Combine(
            AppContext.BaseDirectory,
            "ApprovedR5ModelApi.txt");
        var approved = NormalizeNewlines(File.ReadAllText(approvedPath)).TrimEnd();
        var actual = R5ModelPublicApiSnapshot.Create(
            R5ModelPublicApiSnapshot.GetSurfaceTypes(typeof(FhirObject).Assembly));

        if (string.Equals(approved, "PENDING", StringComparison.Ordinal))
        {
            Assert.Fail(
                "ApprovedR5ModelApi.txt has not been initialized.\n" + actual);
        }

        Assert.Equal(approved, actual);
    }

    [Fact]
    public void R5ModelSurfaceIsCompleteAndExplicit()
    {
        var types = R5ModelPublicApiSnapshot.GetSurfaceTypes(
            typeof(FhirObject).Assembly);

        Assert.Equal(68, types.Count);
        Assert.Equal(
            17,
            types.Count(type => type.Namespace == "MyFhirSdk.Types"));
        Assert.Equal(
            39,
            types.Count(type => type.Namespace == "MyFhirSdk.Resources"));
        Assert.Equal(
            12,
            types.Count(type => type.Namespace == "MyFhirSdk.Core"));
    }

    [Fact]
    public void R5ModelSnapshotIsIndependentOfInputOrder()
    {
        var types = R5ModelPublicApiSnapshot.GetSurfaceTypes(
            typeof(FhirObject).Assembly);
        var original = R5ModelPublicApiSnapshot.Create(types);
        var reversed = R5ModelPublicApiSnapshot.Create(types.Reverse());

        Assert.Equal(original, reversed);
    }

    [Fact]
    public void R5ModelSnapshotCapturesCompatibilityCriticalShape()
    {
        var snapshot = R5ModelPublicApiSnapshot.Create(
            R5ModelPublicApiSnapshot.GetSurfaceTypes(typeof(FhirObject).Assembly));

        Assert.Contains(
            "PROPERTY MyFhirSdk.Core.IFhirExtensionValue? Value | " +
            "get=public | set=public | dispatch=none | JsonName=-",
            snapshot,
            StringComparison.Ordinal);
        Assert.Contains(
            "PROPERTY System.Collections.Generic.IList<MyFhirSdk.Types.HumanName> Name | " +
            "get=public | set=public | dispatch=none | JsonName=-",
            snapshot,
            StringComparison.Ordinal);
        Assert.Contains(
            "PROPERTY MyFhirSdk.Primitives.FhirString? ReferenceValue | " +
            "get=public | set=public | dispatch=none | JsonName=reference",
            snapshot,
            StringComparison.Ordinal);
        Assert.Contains(
            "PROPERTY System.String ResourceType | " +
            "get=public | set=- | dispatch=abstract | JsonName=-",
            snapshot,
            StringComparison.Ordinal);
        Assert.Contains(
            "PROPERTY System.String ResourceType | " +
            "get=public | set=- | dispatch=override | JsonName=resourceType",
            snapshot,
            StringComparison.Ordinal);
    }

    private static string NormalizeNewlines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
