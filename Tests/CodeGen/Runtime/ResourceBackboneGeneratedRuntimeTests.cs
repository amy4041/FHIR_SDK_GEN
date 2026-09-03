using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Tests.Generation;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class ResourceBackboneGeneratedRuntimeTests
{
    [Fact]
    public async Task FullOfficialIr_PreservesEveryExistingResourceNamespaceTypeAndProperty()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var generatedNames = graph.Nodes
            .Where(node => node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel)
            .Select(node => node.FhirTypeName)
            .ToArray();
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(generatedNames);
        var declarations = ir.Declarations.ToDictionary(
            declaration => declaration.FullyQualifiedName,
            StringComparer.Ordinal);
        var existingTypes = typeof(MyFhirSdk.Resources.Patient).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == "MyFhirSdk.Resources")
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(39, existingTypes.Length);
        foreach (var existingType in existingTypes)
        {
            var declaration = declarations[existingType.FullName!];
            Assert.Equal(existingType.IsAbstract, declaration.IsAbstract);
            Assert.Equal(existingType.IsSealed, declaration.IsSealed);
            Assert.Equal(existingType.BaseType?.FullName, declaration.BaseType.ClrType);
            var generatedProperties = declaration.Members
                .SelectMany(member => member.Properties)
                .ToDictionary(property => property.CSharpName, StringComparer.Ordinal);

            foreach (var existingProperty in existingType.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (existingProperty.Name == nameof(Resource.ResourceType))
                {
                    Assert.Equal(existingType.Name, declaration.FhirName);
                    continue;
                }

                var generatedProperty = generatedProperties[existingProperty.Name];
                var isCollection = existingProperty.PropertyType.IsGenericType &&
                    existingProperty.PropertyType.GetGenericTypeDefinition() == typeof(IList<>);
                var existingValueType = isCollection
                    ? existingProperty.PropertyType.GetGenericArguments()[0]
                    : existingProperty.PropertyType;
                Assert.Equal(isCollection, generatedProperty.IsCollection);
                Assert.True(
                    string.Equals(
                        existingValueType.FullName,
                        generatedProperty.CSharpType,
                        StringComparison.Ordinal),
                    $"{existingType.FullName}.{existingProperty.Name}: expected " +
                    $"'{existingValueType.FullName}', generated '{generatedProperty.CSharpType}'.");
                Assert.Equal(
                    GetEffectiveJsonName(existingProperty),
                    generatedProperty.JsonName);
            }
        }
    }

    [Fact]
    public async Task ExistingPatientSurface_RemainsPresentInGeneratedModels()
    {
        var assembly = await CompilePatientClosureAsync();
        var existingTypes = new[]
        {
            typeof(MyFhirSdk.Resources.Patient),
            typeof(MyFhirSdk.Resources.PatientContact),
            typeof(MyFhirSdk.Resources.PatientCommunication)
        };

        foreach (var existingType in existingTypes)
        {
            var generatedType = assembly.GetType(existingType.FullName!, throwOnError: true)!;
            Assert.Equal(existingType.IsAbstract, generatedType.IsAbstract);
            Assert.Equal(existingType.IsSealed, generatedType.IsSealed);
            Assert.Equal(existingType.BaseType?.FullName, generatedType.BaseType?.FullName);
            var generatedShape = PublicShape(generatedType).ToHashSet(StringComparer.Ordinal);
            Assert.Subset(generatedShape, PublicShape(existingType).ToHashSet(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task GeneratedPatient_RoundTripsChoiceAndContainedResource_AndValidatorTraversesIt()
    {
        var assembly = await CompilePatientClosureAsync();
        var patientType = assembly.GetType("MyFhirSdk.Resources.Patient", throwOnError: true)!;
        var patient = Assert.IsAssignableFrom<Resource>(Activator.CreateInstance(patientType));
        patientType.GetProperty("DeceasedBoolean")!.SetValue(patient, new FhirBoolean(true));
        patientType.GetProperty("BirthDate")!.SetValue(patient, new FhirDate("1974-12-25"));
        var contained = Assert.IsAssignableFrom<IList>(
            typeof(DomainResource).GetProperty(nameof(DomainResource.Contained))!.GetValue(patient));
        contained.Add(new MyFhirSdk.Resources.Organization { Name = new FhirString("Example") });
        var serializer = new FhirJsonSerializer();

        var firstJson = serializer.Serialize(patient);
        var parseMethod = typeof(FhirJsonParser)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == nameof(FhirJsonParser.Parse) && method.IsGenericMethod)
            .MakeGenericMethod(patientType);
        var parsed = Assert.IsAssignableFrom<Resource>(
            parseMethod.Invoke(new FhirJsonParser(), [firstJson]));
        var secondJson = serializer.Serialize(parsed);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstJson), JsonNode.Parse(secondJson)));
        Assert.Equal("Patient", parsed.ResourceType);
        Assert.Equal(
            true,
            ((FhirBoolean)patientType.GetProperty("DeceasedBoolean")!.GetValue(parsed)!).Value);
        Assert.Single(Assert.IsAssignableFrom<IList>(
            typeof(DomainResource).GetProperty(nameof(DomainResource.Contained))!.GetValue(parsed)));

        patientType.GetProperty("BirthDate")!.SetValue(parsed, new FhirDate("2020-99-99"));
        var validation = new FhirValidator().Validate(parsed);
        Assert.Contains(validation.Issues, issue =>
            issue.Code == ValidationIssueCode.PrimitiveFormat &&
            issue.Path == "Patient.birthDate");
    }

    private static async Task<Assembly> CompilePatientClosureAsync()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Patient");
        var result = new ResourceBackboneGenerationPipeline().Generate(ir);
        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        return GeneratedModelTestCompiler.Compile(
            Assert.IsType<ResourceBackboneGenerationBatch>(result.Value).Sources);
    }

    private static string[] PublicShape(Type type)
    {
        var nullability = new NullabilityInfoContext();
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => string.Join(
                '|',
                property.Name,
                FormatType(property.PropertyType),
                nullability.Create(property).ReadState,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "-",
                property.SetMethod?.IsPublic == true))
            .ToArray();
    }

    private static string FormatType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }
        var genericName = type.GetGenericTypeDefinition().FullName!;
        return $"{genericName[..genericName.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }

    private static string GetEffectiveJsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
        char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
}
