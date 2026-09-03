using System.Reflection;
using System.Text.Json.Nodes;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Tests.Generation;
using MyFhirSdk.CodeGen.Tests.Metadata;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class ModelMetadataGeneratedRuntimeTests
{
    [Fact]
    public async Task GeneratedComposition_DrivesFactoryOpenTypeRoundTripAndValidation()
    {
        var modelIr = await ModelMetadataTestContext.BuildFullModelIrAsync();
        var result = new ModelMetadataGenerationPipeline().Generate(modelIr);
        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
        var batch = Assert.IsType<ModelMetadataGenerationBatch>(result.Value);
        var assembly = GeneratedModelTestCompiler.Compile(
            batch.CompilationSources.Append(new GeneratedSource(
                "C6RuntimeFacade.cs",
                RuntimeFacadeSource)).ToArray(),
            "MyFhirSdk.Generated.CompilationValidation");
        var facade = assembly.GetType(
            "MyFhirSdk.C6RuntimeFixtures.RuntimeFacade",
            throwOnError: true)!;

        var observationType = Assert.IsType<string>(
            facade.GetMethod("CreateResourceType")!.Invoke(null, ["Observation"]));
        var roundTripJson = Assert.IsType<string>(
            facade.GetMethod("RoundTripTaskOpenType")!.Invoke(null, null));
        var validationPaths = Assert.IsType<string[]>(
            facade.GetMethod("ValidateRequiredTaskInput")!.Invoke(null, null));
        var metaTagType = Assert.IsType<string>(
            facade.GetMethod("ParseEmptyMetaTagType")!.Invoke(null, null));
        var externalValidationPaths = Assert.IsType<string[]>(
            facade.GetMethod("ValidateExternalBootstrapRequirements")!.Invoke(null, null));

        Assert.Equal("MyFhirSdk.Resources.Observation", observationType);
        var json = JsonNode.Parse(roundTripJson)!;
        Assert.Equal("Task", json["resourceType"]!.GetValue<string>());
        Assert.Equal("open-type", json["input"]![0]!["valueString"]!.GetValue<string>());
        Assert.Null(json["input"]![0]!["value"]);
        Assert.Contains("Task.input[0].type", validationPaths);
        Assert.Contains("Task.input[0].value[x]", validationPaths);
        Assert.Equal("MyFhirSdk.Types.Coding", metaTagType);
        Assert.Contains("Patient.extension[0].url", externalValidationPaths);
        Assert.Contains("Patient.text.status", externalValidationPaths);
        Assert.Contains("Patient.text.div", externalValidationPaths);
    }

    private const string RuntimeFacadeSource =
        """
        using System.Linq;
        using MyFhirSdk.Core;
        using MyFhirSdk.ModelMetadata.R5;
        using MyFhirSdk.Primitives;
        using MyFhirSdk.Serialization.Json;
        using MyFhirSdk.Types;
        using MyFhirSdk.Validation;
        using MyFhirSdk.Validation.Traversal;

        namespace MyFhirSdk.C6RuntimeFixtures;

        public static class RuntimeFacade
        {
            public static string CreateResourceType(string fhirTypeName)
            {
                var provider = GeneratedR5ModelMetadata.Create();
                return provider.GetRequiredResource(fhirTypeName)
                    .CreateResource()
                    .GetType()
                    .FullName!;
            }

            public static string RoundTripTaskOpenType()
            {
                var provider = GeneratedR5ModelMetadata.Create();
                var task = new MyFhirSdk.Resources.Task
                {
                    Status = new FhirCode("requested"),
                    Intent = new FhirCode("order")
                };
                task.Input.Add(new MyFhirSdk.Resources.TaskInput
                {
                    Type = new CodeableConcept(),
                    Value = new FhirString("open-type")
                });
                var serializer = new FhirJsonSerializer(provider);
                var json = serializer.Serialize(task);
                var parsed = new FhirJsonParser(provider)
                    .Parse<MyFhirSdk.Resources.Task>(json);
                return serializer.Serialize(parsed);
            }

            public static string[] ValidateRequiredTaskInput()
            {
                var task = new MyFhirSdk.Resources.Task
                {
                    Status = new FhirCode("requested"),
                    Intent = new FhirCode("order")
                };
                task.Input.Add(new MyFhirSdk.Resources.TaskInput());
                var validator = new FhirValidator(
                    GeneratedR5ValidationRules.Create(),
                    new FhirObjectGraphWalker());
                return validator.Validate(task).Issues
                    .Select(issue => issue.Path)
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToArray();
            }

            public static string ParseEmptyMetaTagType()
            {
                var provider = GeneratedR5ModelMetadata.Create();
                const string json = "{ \"resourceType\": \"Patient\", \"meta\": { \"tag\": [{}] } }";
                return new FhirJsonParser(provider)
                    .Parse<MyFhirSdk.Resources.Patient>(json)
                    .Meta!
                    .Tag
                    .Single()
                    .GetType()
                    .FullName!;
            }

            public static string[] ValidateExternalBootstrapRequirements()
            {
                var patient = new MyFhirSdk.Resources.Patient
                {
                    Text = new Narrative()
                };
                patient.Extension.Add(new Extension());
                var validator = new FhirValidator(
                    GeneratedR5ValidationRules.Create(),
                    new FhirObjectGraphWalker());
                return validator.Validate(patient).Issues
                    .Select(issue => issue.Path)
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToArray();
            }
        }
        """;
}
