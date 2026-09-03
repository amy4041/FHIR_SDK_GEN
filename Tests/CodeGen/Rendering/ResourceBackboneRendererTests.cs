using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Tests.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Rendering;

public sealed class ResourceBackboneRendererTests
{
    [Fact]
    public async Task Render_OfficialPatient_EmitsFixedResourceTypeAndChoiceProperties()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");
        var patient = Assert.Single(ir.Declarations, declaration =>
            declaration.Category == ModelIrCategory.Resource &&
            declaration.FhirName == "Patient");

        var source = new ResourceBackboneRenderer().Render(patient);

        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenFiles",
            "R5",
            "Resources",
            "Patient.golden.cs.txt");
        Assert.Equal(
            Normalize(await File.ReadAllTextAsync(goldenPath)),
            Normalize(source));

        Assert.Contains("public sealed class Patient : DomainResource", source, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"resourceType\")]", source, StringComparison.Ordinal);
        Assert.Contains("public override string ResourceType => \"Patient\";", source, StringComparison.Ordinal);
        Assert.Contains("public FhirBoolean? DeceasedBoolean { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("public FhirDateTime? DeceasedDateTime { get; set; }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("deceased[x]", source, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', source);
    }

    [Fact]
    public async Task Render_OfficialPatientContact_IsPublicTopLevelSealedBackbone()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");
        var contact = Assert.Single(ir.Declarations, declaration =>
            declaration.Category == ModelIrCategory.Backbone &&
            declaration.BackboneElementId == "Patient.contact");

        var source = new ResourceBackboneRenderer().Render(contact);

        Assert.Equal("PatientContact", contact.CSharpName);
        Assert.Equal("MyFhirSdk.Resources", contact.Namespace);
        Assert.Equal(
            "Generated/R5/Resources/Patient/PatientContact.g.cs",
            contact.ArtifactPath);
        Assert.Contains("public sealed class PatientContact : BackboneElement", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceType", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_NestedClaimBackbone_RemainsTopLevelAndDirectlyInheritsBackboneElement()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Claim");
        var subDetail = Assert.Single(ir.Declarations, declaration =>
            declaration.Category == ModelIrCategory.Backbone &&
            declaration.BackboneElementId == "Claim.item.detail.subDetail");

        var source = new ResourceBackboneRenderer().Render(subDetail);

        Assert.Equal("ClaimSubDetail", subDetail.CSharpName);
        Assert.Contains("public sealed class ClaimSubDetail : BackboneElement", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClaimDetail", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ProfileNarrowedQuantity_PreservesSimpleQuantityClrType()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Claim");
        var item = Assert.Single(ir.Declarations, declaration =>
            declaration.BackboneElementId == "Claim.item");

        var source = new ResourceBackboneRenderer().Render(item);

        Assert.Contains("public SimpleQuantity? Quantity { get; set; }", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ContentReference_UsesResolvedPropertyTypeWithoutRenderingReferenceSyntax()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var resourceName = graph.Nodes
            .Where(node =>
                node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel &&
                node.Kind == "resource" &&
                node.InventoryItem.Definition.Snapshot?.Elements?.Any(element =>
                    !string.IsNullOrWhiteSpace(element.ContentReference)) == true)
            .Select(node => node.FhirTypeName)
            .First();
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(resourceName);
        var declaration = ir.Declarations.First(candidate =>
            candidate.Category is (ModelIrCategory.Resource or ModelIrCategory.Backbone) &&
            candidate.Members.Any(member => member.ContentReference is not null));
        var member = declaration.Members.First(candidate => candidate.ContentReference is not null);

        var source = new ResourceBackboneRenderer().Render(declaration);

        Assert.NotNull(member.ResolvedContentTarget);
        Assert.All(member.Properties, property => Assert.False(string.IsNullOrWhiteSpace(property.CSharpType)));
        Assert.DoesNotContain(member.ContentReference!, source, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
