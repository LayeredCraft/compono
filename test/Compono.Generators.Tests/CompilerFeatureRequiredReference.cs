using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

/// <summary>
/// Builds a metadata-only reference assembly whose <c>CfrLib.CfrData.Cases</c> field is
/// annotated with <c>[CompilerFeatureRequired]</c>. The attribute cannot be applied in
/// source (CS8335), and <c>PersistedAssemblyBuilder</c> emits a core-library assembly
/// reference that Roslyn cannot bind back to the compilation's reference assemblies, so
/// the attribute arguments decode as empty. This builder emits the custom-attribute blob
/// directly and references the same core library identity the compilation uses.
/// </summary>
internal static class CompilerFeatureRequiredReference
{
    public static MetadataReference Create(bool isOptional, IReadOnlyList<MetadataReference> baseReferences)
    {
        var probeTree = CSharpSyntaxTree.ParseText("internal class Probe { }");
        var probeCompilation = CSharpCompilation.Create("CfrLibProbe", [probeTree], baseReferences);
        var coreLibraryIdentity = probeCompilation.GetTypeByMetadataName("System.Object")!.ContainingAssembly.Identity;

        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("CfrLib.dll"), default, default, default);

        metadata.AddAssembly(
            metadata.GetOrAddString("CfrLib"),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            AssemblyHashAlgorithm.None);

        var coreLibraryReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString(coreLibraryIdentity.Name),
            coreLibraryIdentity.Version,
            default,
            metadata.GetOrAddBlob(coreLibraryIdentity.PublicKeyToken.ToArray()),
            default,
            default);

        var objectTypeReference = metadata.AddTypeReference(
            coreLibraryReference,
            metadata.GetOrAddString("System"),
            metadata.GetOrAddString("Object"));

        var featureAttributeTypeReference = metadata.AddTypeReference(
            coreLibraryReference,
            metadata.GetOrAddString("System.Runtime.CompilerServices"),
            metadata.GetOrAddString("CompilerFeatureRequiredAttribute"));

        // ECMA-335 II.22.37: the first TypeDef row must be the <Module> pseudo-type (no namespace, no
        // base type). Its field and method lists start at row 1 but own nothing, because the next
        // TypeDef (CfrData) also starts its field list at row 1.
        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract,
            metadata.GetOrAddString("CfrLib"),
            metadata.GetOrAddString("CfrData"),
            objectTypeReference,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var fieldSignature = new BlobBuilder();
        fieldSignature.WriteByte(0x06);
        fieldSignature.WriteByte(0x1D);
        fieldSignature.WriteByte(0x1C);

        var casesField = metadata.AddFieldDefinition(
            FieldAttributes.Public | FieldAttributes.Static,
            metadata.GetOrAddString("Cases"),
            metadata.GetOrAddBlob(fieldSignature));

        var constructorSignature = new BlobBuilder();
        constructorSignature.WriteByte(0x20);
        constructorSignature.WriteByte(0x01);
        constructorSignature.WriteByte(0x01);
        constructorSignature.WriteByte(0x0E);

        var attributeConstructor = metadata.AddMemberReference(
            featureAttributeTypeReference,
            metadata.GetOrAddString(".ctor"),
            metadata.GetOrAddBlob(constructorSignature));

        metadata.AddCustomAttribute(casesField, attributeConstructor, metadata.GetOrAddBlob(BuildAttributeValue(isOptional)));

        var rootBuilder = new MetadataRootBuilder(metadata, "v4.0.30319");
        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            rootBuilder,
            new BlobBuilder(),
            flags: CorFlags.ILOnly);
        var image = new BlobBuilder();
        peBuilder.Serialize(image);
        return MetadataReference.CreateFromImage(image.ToImmutableArray());
    }

    private static byte[] BuildAttributeValue(bool isOptional)
    {
        var builder = new BlobBuilder();
        builder.WriteUInt16(0x0001);
        WriteSerString(builder, "FutureFeature");
        if (isOptional)
        {
            builder.WriteUInt16(0x0001);
            builder.WriteByte(0x54);
            builder.WriteByte(0x02);
            WriteSerString(builder, "IsOptional");
            builder.WriteByte(0x01);
        }
        else
        {
            builder.WriteUInt16(0x0000);
        }

        return builder.ToArray();
    }

    private static void WriteSerString(BlobBuilder builder, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteCompressedInteger(builder, bytes.Length);
        builder.WriteBytes(bytes);
    }

    private static void WriteCompressedInteger(BlobBuilder builder, int value)
    {
        if (value < 0x80)
        {
            builder.WriteByte((byte)value);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Only short compressed integers are needed for these test strings.");
        }
    }
}
