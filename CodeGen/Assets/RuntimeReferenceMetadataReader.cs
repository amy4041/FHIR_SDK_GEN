using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using MyFhirSdk.CodeGen.Contracts;

namespace MyFhirSdk.CodeGen.Assets;

internal static class RuntimeReferenceMetadataReader
{
    internal static ResolvedRuntimeReference Read(
        string path,
        RuntimeReferenceKind kind,
        bool includeSha256)
    {
        var validatedImage = includeSha256
            ? ImmutableArray.CreateRange(File.ReadAllBytes(path))
            : ImmutableArray<byte>.Empty;
        using Stream stream = includeSha256
            ? new MemoryStream(validatedImage.ToArray(), writable: false)
            : File.OpenRead(path);
        using var peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException("The reference does not contain CLR metadata.");
        }

        var metadata = peReader.GetMetadataReader();
        if (!metadata.IsAssembly)
        {
            throw new BadImageFormatException("The reference is not a CLR assembly.");
        }

        var definition = metadata.GetAssemblyDefinition();
        var publicKey = metadata.GetBlobBytes(definition.PublicKey);
        var identity = new RuntimeAssemblyIdentity(
            metadata.GetString(definition.Name),
            definition.Version.ToString(4),
            GetPublicKeyToken(publicKey, definition.Flags));
        return new ResolvedRuntimeReference(
            Path.GetFullPath(path),
            identity,
            ReadTargetFramework(metadata, definition),
            includeSha256
                ? Convert.ToHexString(SHA256.HashData(validatedImage.AsSpan()))
                    .ToLowerInvariant()
                : null,
            kind,
            validatedImage);
    }

    private static string GetPublicKeyToken(byte[] publicKey, AssemblyFlags flags)
    {
        if (publicKey.Length == 0)
        {
            return "null";
        }
        if ((flags & AssemblyFlags.PublicKey) == 0)
        {
            return Convert.ToHexString(publicKey).ToLowerInvariant();
        }

        var hash = SHA1.HashData(publicKey);
        return Convert.ToHexString(hash[^8..].Reverse().ToArray()).ToLowerInvariant();
    }

    private static string? ReadTargetFramework(
        MetadataReader metadata,
        AssemblyDefinition definition)
    {
        foreach (var handle in definition.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!IsTargetFrameworkAttribute(metadata, attribute.Constructor))
            {
                continue;
            }

            var value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() != 1)
            {
                throw new BadImageFormatException(
                    "The TargetFrameworkAttribute has an invalid metadata signature.");
            }
            return NormalizeTargetFramework(value.ReadSerializedString());
        }
        return null;
    }

    private static bool IsTargetFrameworkAttribute(
        MetadataReader metadata,
        EntityHandle constructor)
    {
        EntityHandle declaringType = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata
                .GetMemberReference((MemberReferenceHandle)constructor)
                .Parent,
            HandleKind.MethodDefinition => metadata
                .GetMethodDefinition((MethodDefinitionHandle)constructor)
                .GetDeclaringType(),
            _ => default
        };
        if (declaringType.IsNil)
        {
            return false;
        }

        return declaringType.Kind switch
        {
            HandleKind.TypeReference => IsTargetFrameworkType(
                metadata,
                metadata.GetTypeReference((TypeReferenceHandle)declaringType)),
            HandleKind.TypeDefinition => IsTargetFrameworkType(
                metadata,
                metadata.GetTypeDefinition((TypeDefinitionHandle)declaringType)),
            _ => false
        };
    }

    private static bool IsTargetFrameworkType(
        MetadataReader metadata,
        TypeReference type) =>
        metadata.GetString(type.Namespace) == "System.Runtime.Versioning" &&
        metadata.GetString(type.Name) == "TargetFrameworkAttribute";

    private static bool IsTargetFrameworkType(
        MetadataReader metadata,
        TypeDefinition type) =>
        metadata.GetString(type.Namespace) == "System.Runtime.Versioning" &&
        metadata.GetString(type.Name) == "TargetFrameworkAttribute";

    private static string? NormalizeTargetFramework(string? frameworkName)
    {
        const string netCorePrefix = ".NETCoreApp,Version=v";
        if (frameworkName?.StartsWith(netCorePrefix, StringComparison.Ordinal) == true)
        {
            return "net" + frameworkName[netCorePrefix.Length..];
        }

        const string netStandardPrefix = ".NETStandard,Version=v";
        if (frameworkName?.StartsWith(netStandardPrefix, StringComparison.Ordinal) == true)
        {
            return "netstandard" + frameworkName[netStandardPrefix.Length..];
        }
        return frameworkName;
    }
}
