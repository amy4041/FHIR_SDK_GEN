using System.Reflection;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Metadata;
using MyFhirSdk.CodeGen.Tests.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Metadata;

public sealed class ModelMetadataIrBuilderTests
{
    [Fact]
    public async Task Build_OfficialSelectedPeriodScope_FiltersUnavailableExtensionTypes()
    {
        var (_, modelIr) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Period");

        var result = new ModelMetadataIrBuilder().Build(modelIr);

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var metadata = Assert.IsType<ModelMetadataIrBatch>(result.Value);
        Assert.Equal(3, metadata.ConcreteDatatypes.Count);
        Assert.Equal(22, metadata.ExtensionValues.Count);
        Assert.Contains(metadata.ExtensionValues, item =>
            item.ClrType == "MyFhirSdk.Primitives.FhirString");
        Assert.Contains(metadata.ExtensionValues, item =>
            item.ClrType == "MyFhirSdk.Core.Meta");
        Assert.Contains(metadata.ExtensionValues, item =>
            item.ClrType == "MyFhirSdk.Types.Period");
        Assert.DoesNotContain(metadata.ExtensionValues, item =>
            item.ClrType == "MyFhirSdk.Types.Age");
    }

    [Fact]
    public async Task Build_OfficialFullScope_CreatesCompleteDeterministicMetadataInventory()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();
        var builder = new ModelMetadataIrBuilder();

        var first = builder.Build(modelIr);
        var second = builder.Build(modelIr);

        Assert.True(first.IsSuccess, ComplexDatatypeTestContext.Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, ComplexDatatypeTestContext.Describe(second.Diagnostics));
        var metadata = Assert.IsType<ModelMetadataIrBatch>(first.Value);
        Assert.Equal(158, metadata.Resources.Count);
        Assert.Equal(41, metadata.ConcreteDatatypes.Count);
        Assert.Equal(2, metadata.DeclaredDatatypes.Count);
        Assert.Equal(54, metadata.ExtensionValues.Count);
        Assert.Equal(486, metadata.OpenTypes.Count);
        var rules = metadata.ValidationTypes.SelectMany(item => item.Rules).ToArray();
        Assert.Equal(794, rules.Count(item => item.Kind == ValidationRuleKind.RequiredScalar));
        Assert.Equal(59, rules.Count(item => item.Kind == ValidationRuleKind.RequiredCollection));
        Assert.Equal(194, rules.Count(item => item.Kind == ValidationRuleKind.ChoiceAtMostOne));
        Assert.Equal(56, rules.Count(item => item.Kind == ValidationRuleKind.ChoiceExactlyOne));
        Assert.Equal(1103, rules.Length);
        Assert.Equal(Snapshot(metadata), Snapshot(Assert.IsType<ModelMetadataIrBatch>(second.Value)));
    }

    [Fact]
    public async Task Build_OfficialFullScope_ContainsFactoryOpenTypeAndValidationExamples()
    {
        var result = new ModelMetadataIrBuilder().Build(
            await ModelMetadataTestContext.BuildFullModelIrAsync());

        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var metadata = Assert.IsType<ModelMetadataIrBatch>(result.Value);
        Assert.Contains(metadata.Resources, item =>
            item.FhirTypeName == "Patient" &&
            item.ClrType == "MyFhirSdk.Resources.Patient");
        Assert.Contains(metadata.ExtensionValues, item =>
            item.FhirTypeCode == "string" &&
            item.JsonPropertyName == "valueString" &&
            item.ClrType == "MyFhirSdk.Primitives.FhirString");
        Assert.Contains(metadata.ConcreteDatatypes, item =>
            item.ClrType == "MyFhirSdk.Core.Meta");
        Assert.Contains(metadata.ConcreteDatatypes, item =>
            item.ClrType == "MyFhirSdk.Core.Narrative");
        Assert.Contains(metadata.DeclaredDatatypes, item =>
            item.DeclaringClrType == "MyFhirSdk.Core.Meta" &&
            item.PropertyName == "security" &&
            item.ConcreteClrType == "MyFhirSdk.Types.Coding");
        Assert.Contains(metadata.DeclaredDatatypes, item =>
            item.DeclaringClrType == "MyFhirSdk.Core.Meta" &&
            item.PropertyName == "tag" &&
            item.ConcreteClrType == "MyFhirSdk.Types.Coding");
        Assert.Contains(metadata.OpenTypes, item =>
            item.ChoiceElementId == "Task.input.value[x]" &&
            item.JsonPropertyName == "valueString" &&
            item.ValueClrType == "MyFhirSdk.Primitives.FhirString");
        Assert.Contains(
            metadata.ValidationTypes.SelectMany(item => item.Rules),
            item =>
                item.ElementId == "Patient.deceased[x]" &&
                item.Kind == ValidationRuleKind.ChoiceAtMostOne);
        Assert.Contains(
            metadata.ValidationTypes.SelectMany(item => item.Rules),
            item =>
                item.Kind == ValidationRuleKind.RequiredCollection &&
                item.ClrPropertyNames.Count == 1);
        Assert.Contains(
            metadata.ValidationTypes.Single(item => item.ClrType == "MyFhirSdk.Core.Extension").Rules,
            item => item.ElementId == "Extension.url" &&
                item.Kind == ValidationRuleKind.RequiredScalar);
        Assert.Equal(
            2,
            metadata.ValidationTypes.Single(item => item.ClrType == "MyFhirSdk.Core.Narrative")
                .Rules.Count(item => item.Kind == ValidationRuleKind.RequiredScalar));
    }

    [Fact]
    public async Task Build_ConflictingOpenTypeSerializerIdentity_FailsBeforeRendering()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();
        var declaration = modelIr.Declarations.Single(item =>
            item.FullyQualifiedName == "MyFhirSdk.Resources.TaskInput");
        var member = declaration.Members.Single(item => item.Source.ElementId == "Task.input.value[x]");
        var alternative = member.TypeAlternatives.Single(item => item.FhirTypeCode == "string");
        var conflict = InvokeInternal<ModelTypeReferenceIr>(
            "uri",
            alternative.TargetCanonical,
            alternative.TargetElementId,
            alternative.ClrType,
            alternative.IsAbstractTarget,
            alternative.IsExternal,
            alternative.IsPrimitive,
            alternative.IsSupported,
            alternative.Profiles,
            alternative.TargetProfiles);
        var conflictingMember = InvokeInternal<ModelMemberIr>(
            member.Source,
            member.FhirName,
            member.JsonName,
            member.Representation,
            member.Cardinality,
            member.ChoiceStem,
            member.ContentReference,
            member.ResolvedContentTarget,
            member.TypeAlternatives.Append(conflict),
            member.Properties,
            member.Validation,
            member.Documentation,
            member.Order);
        var conflictingDeclaration = CloneDeclaration(
            declaration,
            declaration.Members.Select(item => item == member ? conflictingMember : item));
        var conflictingBatch = InvokeInternal<ModelIrBatch>(
            modelIr.Declarations.Select(item => item == declaration ? conflictingDeclaration : item),
            modelIr.ExternalMetadata);

        var result = new ModelMetadataIrBuilder().Build(conflictingBatch);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("open-type serializer identity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Build_ConflictingExtensionClrIdentity_FailsBeforeRendering()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();
        var extension = modelIr.ExternalMetadata.Single(item => item.FhirName == "Extension");
        var value = extension.Members.Single(item => item.Source.ElementId == "Extension.value[x]");
        var alternative = value.TypeAlternatives.Single(item => item.FhirTypeCode == "string");
        var conflict = InvokeInternal<ModelTypeReferenceIr>(
            "uri",
            alternative.TargetCanonical,
            alternative.TargetElementId,
            alternative.ClrType,
            alternative.IsAbstractTarget,
            alternative.IsExternal,
            alternative.IsPrimitive,
            alternative.IsSupported,
            alternative.Profiles,
            alternative.TargetProfiles);
        var conflictingValue = value with
        {
            TypeAlternatives = value.TypeAlternatives.Append(conflict).ToArray()
        };
        var conflictingExtension = InvokeInternal<ExternalModelMetadataIr>(
            extension.Source,
            extension.FhirName,
            extension.ClrType,
            extension.Kind,
            extension.IsAbstract,
            extension.Members.Select(item => item == value ? conflictingValue : item));
        var conflictingBatch = InvokeInternal<ModelIrBatch>(
            modelIr.Declarations,
            modelIr.ExternalMetadata.Select(item => item == extension ? conflictingExtension : item));

        var result = new ModelMetadataIrBuilder().Build(conflictingBatch);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("conflicting FHIR JSON identities", StringComparison.Ordinal));
    }

    private static ModelDeclarationIr CloneDeclaration(
        ModelDeclarationIr declaration,
        IEnumerable<ModelMemberIr> members) =>
        InvokeInternal<ModelDeclarationIr>(
            declaration.Source,
            declaration.Category,
            declaration.FhirName,
            declaration.CSharpName,
            declaration.Namespace,
            declaration.ArtifactPath,
            declaration.IsAbstract,
            declaration.IsSealed,
            declaration.BaseType,
            declaration.ResourceOwnerCanonical,
            declaration.BackboneElementId,
            members);

    private static T InvokeInternal<T>(params object?[] arguments)
    {
        var constructor = Assert.Single(typeof(T).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<T>(constructor.Invoke(arguments));
    }

    private static string[] Snapshot(ModelMetadataIrBatch batch) =>
    [
        .. batch.Resources.Select(item => $"resource|{item.FhirTypeName}|{item.ClrType}"),
        .. batch.ConcreteDatatypes.Select(item => $"datatype|{item.ClrType}"),
        .. batch.DeclaredDatatypes.Select(item =>
            $"declared|{item.DeclaringClrType}|{item.PropertyName}|{item.ConcreteClrType}"),
        .. batch.ExtensionValues.Select(item =>
            $"extension|{item.FhirTypeCode}|{item.ClrType}|{item.JsonPropertyName}"),
        .. batch.OpenTypes.Select(item =>
            $"open|{item.DeclaringClrType}|{item.ClrPropertyName}|{item.JsonPropertyName}|{item.ValueClrType}"),
        .. batch.ValidationTypes.SelectMany(item => item.Rules.Select(rule =>
            $"rule|{item.ClrType}|{rule.ElementId}|{rule.Kind}|{string.Join(',', rule.ClrPropertyNames)}"))
    ];
}
