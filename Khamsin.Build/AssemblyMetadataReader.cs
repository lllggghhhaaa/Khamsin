using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Khamsin.Build;

/// <summary>
/// Reads Khamsin manifests and component usage from compiled metadata. Component usage comes from
/// <c>RenderTreeBuilder.OpenComponent&lt;T&gt;</c> calls, not from IL string literals.
/// </summary>
internal static class AssemblyMetadataReader
{
    private const string ManifestAttributeName = "Khamsin.KhamsinManifestAttribute";
    private const string RenderTreeBuilderName = "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";

    private static readonly Lazy<Dictionary<ushort, OpCodeInfo>> OpCodeTable = new(BuildOpCodeTable);

    internal static AssemblyScanResult Scan(string path, bool scanUsage)
    {
        var result = new AssemblyScanResult();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            return result;
        }

        var reader = peReader.GetMetadataReader();
        ReadAssemblyManifests(reader, result);

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            foreach (var attributeHandle in type.GetCustomAttributes())
            {
                var manifest = ReadManifest(reader, attributeHandle);
                if (manifest is null)
                    continue;

                var typeName = TypeNames.FromDefinition(reader, typeHandle);
                if (result.Manifests.TryGetValue(typeName, out var existing))
                {
                    var uses = new List<string>(existing.Uses);
                    
                    uses.AddRange(manifest.Uses.Where(use => !uses.Contains(use)));

                    result.Manifests[typeName] = new ManifestInfo(existing.Classes + " " + manifest.Classes, uses);
                }
                else
                {
                    result.Manifests[typeName] = manifest;
                }
            }

            if (scanUsage) ScanTypeUsage(peReader, reader, type, result.UsedComponents);
        }

        return result;
    }

    private static void ReadAssemblyManifests(MetadataReader reader, AssemblyScanResult result)
        => result.AssemblyManifests.AddRange(
            reader.GetAssemblyDefinition().GetCustomAttributes().Select(attributeHandle =>
                ReadManifest(reader, attributeHandle)).OfType<ManifestInfo>());

    private static ManifestInfo? ReadManifest(MetadataReader reader, CustomAttributeHandle handle)
    {
        var attribute = reader.GetCustomAttribute(handle);
        var attributeType = TypeNames.FromAttributeType(reader, attribute);
        if (!string.Equals(attributeType, ManifestAttributeName, StringComparison.Ordinal))
            return null;

        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.Length < 4)
            return null;

        if (blob.ReadUInt16() != 0x0001)
            return null;

        var namedCount = blob.ReadUInt16();
        var classes = string.Empty;
        var uses = new List<string>();

        for (var i = 0; i < namedCount; i++)
        {
            if (blob.RemainingBytes < 2)
                break;

            blob.ReadByte();
            var elementType = blob.ReadByte();
            var isArray = false;
            if (elementType == 0x1D)
            {
                isArray = true;
                elementType = blob.ReadByte();
            }

            var name = blob.ReadSerializedString();
            if (name is null)
                break;

            if (string.Equals(name, "Classes", StringComparison.Ordinal))
            {
                classes = blob.ReadSerializedString() ?? string.Empty;
            }
            else if (string.Equals(name, "Uses", StringComparison.Ordinal) && isArray && elementType == 0x50)
            {
                var count = blob.ReadUInt32();
                for (var u = 0u; u < count && blob.RemainingBytes > 0; u++)
                {
                    var typeName = TypeNames.CleanAttributeTypeName(blob.ReadSerializedString());
                    if (typeName != null) uses.Add(typeName);
                }
            }
            else
            {
                SkipValue(blob, elementType, isArray);
            }
        }

        return new ManifestInfo(classes, uses);
    }

    private static void SkipValue(BlobReader blob, byte elementType, bool isArray)
    {
        if (isArray)
        {
            var count = blob.ReadUInt32();
            for (var i = 0u; i < count && blob.RemainingBytes > 0; i++) 
                blob.ReadSerializedString();

            return;
        }

        if (elementType is 0x0E or 0x50) blob.ReadSerializedString();
    }

    private static void ScanTypeUsage(PEReader peReader, MetadataReader reader, TypeDefinition type, HashSet<string> used)
    {
        foreach (var method in type.GetMethods().Select(reader.GetMethodDefinition).Where(method => method.RelativeVirtualAddress != 0))
        {
            MethodBodyBlock body;
            try
            {
                body = peReader.GetMethodBody(method.RelativeVirtualAddress);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            ScanIl(reader, body.GetILContent(), used);
        }
    }

    private static void ScanIl(MetadataReader reader, ImmutableArray<byte> il, HashSet<string> used)
    {
        var offset = 0;
        while (offset < il.Length)
        {
            ushort code = il[offset++];
            if (code == 0xFE)
            {
                if (offset >= il.Length)
                    return;

                code = (ushort)(0xFE00 | il[offset++]);
            }

            if (!OpCodeTable.Value.TryGetValue(code, out var info))
                return;

            if (info is { OperandType: OperandType.InlineMethod, Name: "call" or "callvirt" or "newobj" })
            {
                if (offset + 4 > il.Length)
                {
                    return;
                }

                var token = ReadInt32(il, offset);
                HandleMethodToken(reader, token, used);
            }

            if (info.OperandType == OperandType.InlineSwitch)
            {
                if (offset + 4 > il.Length)
                {
                    return;
                }

                var count = (uint)ReadInt32(il, offset);
                offset += 4;
                offset += (int)count * 4;
                continue;
            }

            offset += OperandSize(info.OperandType);
            if (offset > il.Length)
            {
                return;
            }
        }
    }

    private static void HandleMethodToken(MetadataReader reader, int token, HashSet<string> used)
    {
        EntityHandle handle;
        try
        {
            handle = MetadataTokens.EntityHandle(token);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (handle.Kind is not HandleKind.MethodSpecification)
            return;

        MethodSpecification specification;
        try
        {
            specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
        }
        catch (BadImageFormatException)
        {
            return;
        }

        var methodName = TypeNames.GetMethodName(reader, specification.Method);
        if (!string.Equals(methodName, "OpenComponent", StringComparison.Ordinal))
            return;

        var parentType = TypeNames.GetMethodParentTypeName(reader, specification.Method);
        if (!string.Equals(parentType, RenderTreeBuilderName, StringComparison.Ordinal))
            return;

        ImmutableArray<string> arguments;
        try
        {
            arguments = specification.DecodeSignature(new ComponentTypeProvider(), null);
        }
        catch (BadImageFormatException)
        {
            return;
        }

        if (arguments.Length == 0)
            return;

        var component = arguments[0];
        if (component.Length > 0 && component.IndexOf('<') < 0) used.Add(component);
    }

    private static int ReadInt32(ImmutableArray<byte> bytes, int offset)
        => bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);

    private static int OperandSize(OperandType type) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.ShortInlineR => 4,
        _ => 4,
    };

    private static Dictionary<ushort, OpCodeInfo> BuildOpCodeTable()
    {
        var table = new Dictionary<ushort, OpCodeInfo>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(OpCode))
                continue;

            var opCode = (OpCode)field.GetValue(null)!;
            var key = unchecked((ushort)opCode.Value);
            table[key] = new OpCodeInfo(opCode.Name ?? field.Name, opCode.OperandType);
        }

        return table;
    }

    private readonly record struct OpCodeInfo(string Name, OperandType OperandType);
}

/// <summary>Resolves metadata type names and method parents.</summary>
internal static class TypeNames
{
    internal static string FromDefinition(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
            return FromDefinition(reader, declaring) + "+" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    internal static string FromReference(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        var name = reader.GetString(type.Name);
        var scope = type.ResolutionScope;
        if (scope.Kind is HandleKind.TypeReference)
            return FromReference(reader, (TypeReferenceHandle)scope) + "+" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    internal static string FromEntity(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return FromDefinition(reader, (TypeDefinitionHandle)handle);
            case HandleKind.TypeReference:
                return FromReference(reader, (TypeReferenceHandle)handle);
            case HandleKind.TypeSpecification:
                try
                {
                    var specification = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    return specification.DecodeSignature(new ComponentTypeProvider(), null);
                }
                catch (BadImageFormatException)
                {
                    return string.Empty;
                }

            default:
                return string.Empty;
        }
    }

    internal static string FromAttributeType(MetadataReader reader, CustomAttribute attribute)
    {
        var constructor = attribute.Constructor;
        switch (constructor.Kind)
        {
            case HandleKind.MemberReference:
                return FromEntity(reader, reader.GetMemberReference((MemberReferenceHandle)constructor).Parent);
            case HandleKind.MethodDefinition:
                return FromDefinition(reader, reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType());
            default:
                return string.Empty;
        }
    }

    internal static string GetMethodName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind is HandleKind.MemberReference)
            return reader.GetString(reader.GetMemberReference((MemberReferenceHandle)handle).Name);

        if (handle.Kind is HandleKind.MethodDefinition)
            return reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)handle).Name);

        return string.Empty;
    }

    internal static string GetMethodParentTypeName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind is HandleKind.MemberReference)
            return FromEntity(reader, reader.GetMemberReference((MemberReferenceHandle)handle).Parent);

        if (handle.Kind is HandleKind.MethodDefinition)
            return FromDefinition(reader, reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType());

        return string.Empty;
    }

    internal static string? CleanAttributeTypeName(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var comma = value.IndexOf(',');
        var name = (comma < 0 ? value : value[..comma]).Trim();
        if (name.StartsWith("global::", StringComparison.Ordinal)) name = name[8..];

        if (name.Length == 0 || name.Contains('`') || name.Contains('['))
            return null;

        return name;
    }
}

/// <summary>Decodes signature blobs into readable metadata type names.</summary>
internal sealed class ComponentTypeProvider : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => TypeNames.FromDefinition(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => TypeNames.FromReference(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape)
        => elementType + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]";

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var builder = new System.Text.StringBuilder(genericType);
        builder.Append('<');
        for (var i = 0; i < typeArguments.Length; i++)
        {
            if (i > 0) builder.Append(',');

            builder.Append(typeArguments[i]);
        }

        return builder.Append('>').ToString();
    }

    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
}
