using System.Text.Json;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Rendering;

internal static class GenerationManifestProvenanceRenderer
{
    internal static void Write(
        Utf8JsonWriter writer,
        GenerationManifestProvenance provenance)
    {
        writer.WriteStartObject("compatibility");
        writer.WriteNumber("schemaVersion", provenance.CompatibilitySchemaVersion);
        writer.WriteString("versionPolicy", provenance.CompatibilityVersionPolicy);
        writer.WriteStartObject("tool");
        writer.WriteString("packageId", provenance.ToolPackageId);
        writer.WriteString("version", provenance.ToolVersion);
        writer.WriteEndObject();
        writer.WriteString("codeGenVersion", provenance.CodeGenVersion);
        writer.WriteStartObject("runtimeDescriptor");
        writer.WriteNumber("schemaVersion", provenance.RuntimeDescriptorSchemaVersion);
        writer.WriteString("version", provenance.RuntimeDescriptorVersion);
        writer.WriteString("sha256", provenance.RuntimeDescriptorSha256);
        writer.WriteEndObject();
        writer.WriteStartObject("compilerReference");
        writer.WriteString("logicalIdentity", provenance.CompilerReferenceLogicalIdentity);
        writer.WriteString("sha256", provenance.CompilerReferenceSha256);
        writer.WriteEndObject();
        writer.WriteString("targetFramework", provenance.TargetFramework);
        writer.WriteEndObject();
    }
}
