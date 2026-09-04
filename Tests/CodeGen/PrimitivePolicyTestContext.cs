using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

internal static class PrimitivePolicyTestContext
{
    private static readonly Lazy<PrimitiveTypeMappingView> MappingView = new(
        LoadMappingView);

    internal static CSharpTypeMapper CreateTypeMapper() =>
        new(
            MappingView.Value,
            new DefinitionTypeMappingView(KnownComplexTypeNames.Select(typeName =>
                new DefinitionTypeMapping(typeName, typeName, "MyFhirSdk.Types"))));

    internal static PrimitiveTypeMappingView GetMappingView() =>
        MappingView.Value;

    private static readonly string[] KnownComplexTypeNames =
    [
        "Address",
        "Attachment",
        "CodeableConcept",
        "CodeableReference",
        "Coding",
        "ContactPoint",
        "Duration",
        "ExtendedContactDetail",
        "HumanName",
        "Identifier",
        "Money",
        "Period",
        "Quantity",
        "Reference",
        "Signature",
        "SimpleQuantity",
        "VirtualServiceDetail"
    ];

    private static PrimitiveTypeMappingView LoadMappingView()
    {
        var policyPath = Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json");
        var loadResult = new PrimitiveGenerationPolicyLoader()
            .LoadAsync(policyPath)
            .GetAwaiter()
            .GetResult();
        Assert.True(
            loadResult.IsSuccess,
            string.Join(Environment.NewLine, loadResult.Diagnostics));
        var validationResult = new PrimitiveGenerationPolicyValidator().Validate(
            Assert.IsType<PrimitiveGenerationPolicyDocument>(loadResult.Value),
            policyPath);
        Assert.True(
            validationResult.IsSuccess,
            string.Join(Environment.NewLine, validationResult.Diagnostics));
        return new PrimitiveTypeMappingView(
            Assert.IsType<ValidatedPrimitiveGenerationPolicy>(
                validationResult.Value));
    }
}
