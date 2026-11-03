using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelMetadata.R5;

var definition = R5ModelProvider.Metadata.Resources.Single(r => r.FhirTypeName == "Patient");
var patient = definition.Factory();
if (patient.GetType() != typeof(MyFhirSdk.Resources.Patient)) throw new Exception("Factory identity mismatch.");
var primitive = R5ModelProvider.Primitives.Definitions.Single(p => p.FhirTypeName == "string");
using var json = JsonDocument.Parse("\"independent\"");
var value = primitive.Codec.CreatePrimitive(primitive.PrimitiveType, json.RootElement);
if (!primitive.Validator.IsValid(value)) throw new Exception("Primitive validation failed.");
var observation = new MyFhirSdk.Resources.Observation();
var issues = R5ModelProvider.Validation.Rules.Where(r => r.AppliesTo.IsInstanceOfType(observation))
    .SelectMany(r => r.Evaluate(new ModelValidationNode(observation, "Observation"))).ToArray();
if (!issues.Any(i => i.Path == "Observation.status")) throw new Exception("Version rules were not composed.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MyFhirSdk")) throw new Exception("SDK was loaded by provider-only consumer.");
Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", sdkLoaded = false,
    models = patient.GetType().Assembly.GetName().Name, primitive = ((IPrimitiveValueAccessor)value).UntypedValue,
    version = R5ModelProvider.Identity.FhirVersion, ruleIssues = issues.Length }));
