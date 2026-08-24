using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitiveRegistryModelBuilderTests
{
    [Fact]
    public async Task Build_WithOfficialCoverage_ProducesOrderedHandoffMatrix()
    {
        var coverage = await LoadCoverageAsync();

        var result = new PrimitiveRegistryModelBuilder().Build(coverage);

        Assert.True(result.IsSuccess);
        var model = Assert.IsType<PrimitiveRegistryCompositionModel>(result.Value);
        Assert.Equal("MyFhirSdk.Primitives", model.Namespace);
        Assert.Equal("PrimitiveRegistry.Composition.g.cs", model.FileName);
        Assert.Equal(17, model.Entries.Count);
        Assert.Equal(
            model.Entries
                .Select(entry => entry.FhirTypeName)
                .Order(StringComparer.Ordinal),
            model.Entries.Select(entry => entry.FhirTypeName));
        Assert.DoesNotContain(
            model.Entries,
            entry => entry.FhirTypeName is "oid" or "time" or "uuid" or "xhtml");

        var decimalEntry = Assert.Single(
            model.Entries,
            entry => entry.FhirTypeName == "decimal");
        Assert.Equal("FhirDecimal", decimalEntry.WrapperName);
        Assert.Equal("decimal?", decimalEntry.ClrValueType);
        Assert.Equal("PrimitiveCodecs.Decimal", decimalEntry.CodecSymbol);
        Assert.Equal("PrimitiveValidators.Decimal", decimalEntry.ValidatorSymbol);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitiveRegistryEntryModel>)model.Entries).Clear());
    }

    public static TheoryData<PrimitiveCodecKey, string> CodecSymbols => new()
    {
        { PrimitiveCodecKey.String, "PrimitiveCodecs.String" },
        { PrimitiveCodecKey.Boolean, "PrimitiveCodecs.Boolean" },
        { PrimitiveCodecKey.Integer, "PrimitiveCodecs.Integer" },
        { PrimitiveCodecKey.DecimalLiteral, "PrimitiveCodecs.Decimal" },
        { PrimitiveCodecKey.Integer64Literal, "PrimitiveCodecs.Integer64" }
    };

    [Theory]
    [MemberData(nameof(CodecSymbols))]
    public void Resolve_MapsEveryCodecKey(
        PrimitiveCodecKey codecKey,
        string expectedSymbol)
    {
        var result = new PrimitiveRuntimeSymbolResolver().Resolve(
            codecKey,
            PrimitiveValidatorKey.String,
            "policy.json",
            "sample");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedSymbol, result.Value?.CodecSymbol);
    }

    [Fact]
    public void Resolve_MapsEveryValidatorKey()
    {
        var resolver = new PrimitiveRuntimeSymbolResolver();
        var keys = Enum.GetValues<PrimitiveValidatorKey>();

        var results = keys.Select(key => resolver.Resolve(
            PrimitiveCodecKey.String,
            key,
            "policy.json",
            key.ToString())).ToArray();

        Assert.Equal(17, keys.Length);
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(
            keys.Select(key => $"PrimitiveValidators.{key}"),
            results.Select(result => result.Value?.ValidatorSymbol));
    }

    [Fact]
    public void Resolve_WithUnknownKeys_ReturnsFsg0025()
    {
        var result = new PrimitiveRuntimeSymbolResolver().Resolve(
            (PrimitiveCodecKey)int.MaxValue,
            (PrimitiveValidatorKey)int.MaxValue,
            "policy.json",
            "sample");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.InvalidPrimitiveRegistryModel,
            diagnostic.Code);
        Assert.Contains("codec key", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("validator key", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositionModel_RejectsDuplicateFhirAndWrapperNames()
    {
        var entry = new PrimitiveRegistryEntryModel(
            "sample",
            "FhirSample",
            "string",
            "PrimitiveCodecs.String",
            "PrimitiveValidators.String");

        Assert.Throws<ArgumentException>(() =>
            new PrimitiveRegistryCompositionModel(
                "MyFhirSdk.Primitives",
                [entry, entry]));
        Assert.Throws<ArgumentException>(() =>
            new PrimitiveRegistryCompositionModel(
                "MyFhirSdk.Primitives",
                [
                    entry,
                    entry with
                    {
                        FhirTypeName = "other"
                    }
                ]));
    }

    internal static async Task<PrimitiveInventoryPolicyCoverage> LoadCoverageAsync()
    {
        var result = await new PrimitiveInventoryCoveragePipeline().BuildAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "StructureDefinitions",
                "Primitives",
                "R5"),
            Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "primitive-generation-policy.json"),
            "5.0.0");

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveInventoryPolicyCoverage>(result.Value);
    }
}
