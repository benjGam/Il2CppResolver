using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes a field discovered through the live IL2CPP runtime.
/// This immutable runtime model associates a native <c>FieldInfo*</c> with its semantic name, managed type, metadata attributes and raw runtime storage offset.
/// It does not attempt to convert static-field offsets into absolute addresses because static storage ownership belongs to a separate runtime-specific concern.
/// </summary>
internal sealed record Il2CppFieldInfo
{
    /// <summary>
    /// Represents the pointer-sized form of the IL2CPP thread-static field offset sentinel.
    /// IL2CPP stores thread-static fields with an offset of negative one instead of a normal instance or static-storage offset.
    /// </summary>
    private static readonly nuint ThreadStaticFieldOffset = unchecked((nuint)(nint)(-1));

    /// <summary>
    /// Gets the native address of the runtime <c>FieldInfo</c> structure representing this field.
    /// </summary>
    public nint FieldAddress { get; }

    /// <summary>
    /// Gets the semantic managed field name exposed by IL2CPP.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the semantic managed type name associated with the field.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the metadata attributes reported by the IL2CPP runtime for this field.
    /// </summary>
    public FieldAttributes Attributes { get; }

    /// <summary>
    /// Gets the raw pointer-sized storage offset reported by <c>il2cpp_field_get_offset</c>.
    /// Its interpretation depends on whether the field is an instance, static, thread-static or literal field.
    /// </summary>
    public nuint Offset { get; }

    /// <summary>
    /// Gets a value indicating whether the field carries the managed static attribute.
    /// </summary>
    public bool IsStatic => (Attributes & FieldAttributes.Static) != 0;

    /// <summary>
    /// Gets a value indicating whether the field represents a metadata literal rather than ordinary runtime storage.
    /// </summary>
    public bool IsLiteral => (Attributes & FieldAttributes.Literal) != 0;

    /// <summary>
    /// Gets a value indicating whether the field uses IL2CPP thread-local static storage.
    /// </summary>
    public bool IsThreadStatic => IsStatic && Offset == ThreadStaticFieldOffset;

    /// <summary>
    /// Initializes an immutable runtime field description.
    /// </summary>
    /// <param name="fieldAddress">The native <c>FieldInfo*</c> address.</param>
    /// <param name="name">The semantic managed field name.</param>
    /// <param name="typeName">The semantic managed field type name.</param>
    /// <param name="attributes">The managed metadata attributes associated with the field.</param>
    /// <param name="offset">The raw runtime storage offset reported by IL2CPP.</param>
    internal Il2CppFieldInfo(nint fieldAddress, string name, string typeName, FieldAttributes attributes, nuint offset)
    {
        if (fieldAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldAddress), "The IL2CPP field address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        FieldAddress = fieldAddress;
        Name = name;
        TypeName = typeName;
        Attributes = attributes;
        Offset = offset;
    }
}