using System.Text;
using System.Text.Json;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Rendering;

public sealed class PrimitiveGenerationManifestRenderer
{
    public string Render(PrimitiveGenerationManifestModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", model.SchemaVersion);
            writer.WriteString("fhirSpecification", model.FhirSpecification);
            writer.WriteString("fhirPackageId", model.FhirPackageId);
            writer.WriteString("fhirPackageVersion", model.FhirPackageVersion);
            writer.WriteString("fhirVersion", model.FhirVersion);
            writer.WriteString("policyVersion", model.PolicyVersion);
            writer.WriteString("codeGenVersion", model.CodeGenVersion);
            writer.WriteString("runtimeContractVersion", model.RuntimeContractVersion);
            writer.WriteString("primitiveNamespace", model.PrimitiveNamespace);
            writer.WriteStartArray("primitives");
            foreach (var primitive in model.Primitives)
            {
                writer.WriteStartObject();
                writer.WriteString("fhirTypeName", primitive.FhirTypeName);
                writer.WriteString("canonical", primitive.Canonical);
                writer.WriteString("fhirVersion", primitive.FhirVersion);
                writer.WriteString("supportStatus", primitive.SupportStatus);
                if (primitive.UnsupportedReason is null)
                {
                    writer.WriteNull("unsupportedReason");
                }
                else
                {
                    writer.WriteString("unsupportedReason", primitive.UnsupportedReason);
                }

                if (primitive.WrapperName is null)
                {
                    writer.WriteNull("wrapperName");
                }
                else
                {
                    writer.WriteString("wrapperName", primitive.WrapperName);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("artifacts");
            foreach (var artifact in model.Artifacts)
            {
                writer.WriteStartObject();
                writer.WriteString("fileName", artifact.FileName);
                writer.WriteString("sha256", artifact.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}
