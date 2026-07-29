using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Models;

public sealed class InternalModelTests
{
    [Fact]
    public void CardinalityModel_PreservesResolvedCardinality()
    {
        var cardinality = new CardinalityModel(
            min: 1,
            max: "*",
            isCollection: true,
            isRequired: true);

        Assert.Equal(1, cardinality.Min);
        Assert.Equal("*", cardinality.Max);
        Assert.True(cardinality.IsCollection);
        Assert.True(cardinality.IsRequired);
    }

    [Fact]
    public void FhirPropertyModel_ExposesPropertyAndCardinalityDecisions()
    {
        var cardinality = new CardinalityModel(
            min: 0,
            max: "*",
            isCollection: true,
            isRequired: false);

        var property = new FhirPropertyModel(
            elementId: "HumanName.given",
            elementPath: "HumanName.given",
            fhirName: "given",
            cSharpName: "Given",
            cSharpType: "MyFhirSdk.Primitives.FhirString",
            cardinality: cardinality,
            documentation: "Given names.",
            order: 3);

        Assert.Equal("HumanName.given", property.ElementId);
        Assert.Equal("HumanName.given", property.ElementPath);
        Assert.Equal("given", property.FhirName);
        Assert.Equal("Given", property.CSharpName);
        Assert.Equal("MyFhirSdk.Primitives.FhirString", property.CSharpType);
        Assert.Same(cardinality, property.Cardinality);
        Assert.True(property.IsCollection);
        Assert.False(property.IsRequired);
        Assert.Equal(0, property.Min);
        Assert.Equal("*", property.Max);
        Assert.Equal("Given names.", property.Documentation);
        Assert.Equal(3, property.Order);
    }

    [Fact]
    public void FhirTypeModel_PreservesTypeMetadataAndPropertyOrder()
    {
        var family = CreateProperty(
            elementId: "HumanName.family",
            fhirName: "family",
            cSharpName: "Family",
            order: 0);
        var given = CreateProperty(
            elementId: "HumanName.given",
            fhirName: "given",
            cSharpName: "Given",
            order: 1);

        var model = new FhirTypeModel(
            fhirName: "HumanName",
            cSharpName: "HumanName",
            @namespace: "MyFhirSdk.GeneratorFixtures.Types",
            cSharpBaseType: "MyFhirSdk.Core.DataType",
            isAbstract: false,
            sourceCanonical: "http://hl7.org/fhir/StructureDefinition/HumanName",
            sourceVersion: "5.0.0",
            properties: [family, given]);

        Assert.Equal("HumanName", model.FhirName);
        Assert.Equal("HumanName", model.CSharpName);
        Assert.Equal("MyFhirSdk.GeneratorFixtures.Types", model.Namespace);
        Assert.Equal("MyFhirSdk.Core.DataType", model.CSharpBaseType);
        Assert.False(model.IsAbstract);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/HumanName",
            model.SourceCanonical);
        Assert.Equal("5.0.0", model.SourceVersion);
        Assert.Equal([family, given], model.Properties);
    }

    [Fact]
    public void FhirTypeModel_CopiesPropertySequence()
    {
        var sourceProperties = new List<FhirPropertyModel>
        {
            CreateProperty(
                elementId: "Period.start",
                fhirName: "start",
                cSharpName: "Start",
                order: 0)
        };

        var model = new FhirTypeModel(
            fhirName: "Period",
            cSharpName: "Period",
            @namespace: "MyFhirSdk.GeneratorFixtures.Types",
            cSharpBaseType: "MyFhirSdk.Core.DataType",
            isAbstract: false,
            sourceCanonical: "http://hl7.org/fhir/StructureDefinition/Period",
            sourceVersion: "5.0.0",
            properties: sourceProperties);

        sourceProperties.Clear();

        Assert.Single(model.Properties);
        Assert.Equal("Period.start", model.Properties[0].ElementId);
    }

    [Fact]
    public void Models_RejectInvalidRequiredValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CardinalityModel(-1, "1", false, false));
        Assert.Throws<ArgumentException>(
            () => new CardinalityModel(0, "", false, false));
        Assert.Throws<ArgumentException>(
            () => CreateProperty(
                elementId: "",
                fhirName: "family",
                cSharpName: "Family",
                order: 0));
    }

    private static FhirPropertyModel CreateProperty(
        string elementId,
        string fhirName,
        string cSharpName,
        int order)
    {
        return new FhirPropertyModel(
            elementId: elementId,
            elementPath: elementId,
            fhirName: fhirName,
            cSharpName: cSharpName,
            cSharpType: "MyFhirSdk.Primitives.FhirString",
            cardinality: new CardinalityModel(
                min: 0,
                max: "1",
                isCollection: false,
                isRequired: false),
            documentation: null,
            order: order);
    }
}
