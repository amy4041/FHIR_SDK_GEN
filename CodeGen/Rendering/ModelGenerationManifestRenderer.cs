using System.Text;
using System.Text.Json;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Rendering;

public sealed class ModelGenerationManifestRenderer
{
    public string Render(ModelGenerationManifestModel model)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", model.SchemaVersion);
            writer.WriteStartObject("package");
            writer.WriteString("id", model.PackageId);
            writer.WriteString("version", model.PackageVersion);
            writer.WriteString("fhirVersion", model.FhirVersion);
            writer.WriteString("sha256", model.PackageSha256);
            writer.WriteEndObject();
            writer.WriteStartObject("primitivePolicy");
            writer.WriteString("version", model.PrimitivePolicyVersion);
            writer.WriteString("sha256", model.PrimitivePolicySha256);
            writer.WriteEndObject();
            writer.WriteString("codeGenVersion", model.CodeGenVersion);
            writer.WriteString("runtimeContractVersion", model.RuntimeContractVersion);
            writer.WriteStartObject("generationScope");
            writer.WriteString("mode", model.Scope);
            writer.WriteStartArray("selectedCanonicals");
            foreach (var canonical in model.SelectedCanonicals) writer.WriteStringValue(canonical);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartArray("modelPolicies");
            foreach (var policy in model.ModelPolicies)
            {
                writer.WriteStartObject(); writer.WriteString("name", policy.Name); writer.WriteString("sha256", policy.Sha256); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartObject("artifactInventory");
            writer.WriteNumber("count", model.Artifacts.Count);
            writer.WriteStartArray("artifacts");
            foreach (var artifact in model.Artifacts)
            {
                writer.WriteStartObject(); writer.WriteString("path", artifact.Path); writer.WriteString("sha256", artifact.Sha256); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
            writer.WriteStartArray("deferredCapabilities");
            foreach (var capability in model.DeferredCapabilities)
            {
                writer.WriteStartObject(); writer.WriteString("id", capability.Id); writer.WriteString("status", capability.Status); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}
