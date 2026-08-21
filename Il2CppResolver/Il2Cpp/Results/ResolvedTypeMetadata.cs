using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents cached public metadata for a resolved IL2CPP type.
/// The result is a pure immutable snapshot and does not perform additional navigation or remote runtime work when its properties are read.
/// </summary>
public sealed class ResolvedTypeMetadata
{
    /// <summary>Gets the managed type attributes reported by IL2CPP.</summary>
    public TypeAttributes Attributes { get; }
    /// <summary>Gets the metadata token associated with the type.</summary>
    public uint MetadataToken { get; }
    /// <summary>Gets a value indicating whether the type is a value type.</summary>
    public bool IsValueType { get; }
    /// <summary>Gets a value indicating whether the type is an enum.</summary>
    public bool IsEnum { get; }
    /// <summary>Gets a value indicating whether IL2CPP reports the type as blittable.</summary>
    public bool IsBlittable { get; }
    /// <summary>Gets a value indicating whether the type is an interface.</summary>
    public bool IsInterface => (Attributes & TypeAttributes.Interface) != 0;
    /// <summary>Gets a value indicating whether the type is abstract.</summary>
    public bool IsAbstract => (Attributes & TypeAttributes.Abstract) != 0;
    /// <summary>Gets a value indicating whether the type is sealed.</summary>
    public bool IsSealed => (Attributes & TypeAttributes.Sealed) != 0;
    /// <summary>Gets a value indicating whether the type is a generic type definition.</summary>
    public bool IsGeneric { get; }
    /// <summary>Gets a value indicating whether the type is an inflated generic instantiation.</summary>
    public bool IsInflated { get; }
    /// <summary>Gets the native size in bytes of a value type, or <see langword="null"/> for reference types.</summary>
    public int? ValueSize { get; }
    /// <summary>Gets the native alignment of a value type, or <see langword="null"/> for reference types.</summary>
    public uint? ValueAlignment { get; }

    /// <summary>Initializes an immutable public type-metadata snapshot.</summary>
    /// <param name="attributes">The managed type attributes.</param>
    /// <param name="metadataToken">The metadata token associated with the type.</param>
    /// <param name="isValueType">Whether the type is a value type.</param>
    /// <param name="isEnum">Whether the type is an enum.</param>
    /// <param name="isBlittable">Whether the type is blittable according to IL2CPP.</param>
    /// <param name="isGeneric">Whether the type is a generic type definition.</param>
    /// <param name="isInflated">Whether the type is an inflated generic instantiation.</param>
    /// <param name="valueSize">The value size when applicable.</param>
    /// <param name="valueAlignment">The value alignment when applicable.</param>
    internal ResolvedTypeMetadata(TypeAttributes attributes, uint metadataToken, bool isValueType, bool isEnum, bool isBlittable, bool isGeneric, bool isInflated, int? valueSize, uint? valueAlignment)
    {
        Attributes = attributes;
        MetadataToken = metadataToken;
        IsValueType = isValueType;
        IsEnum = isEnum;
        IsBlittable = isBlittable;
        IsGeneric = isGeneric;
        IsInflated = isInflated;
        ValueSize = valueSize;
        ValueAlignment = valueAlignment;
    }
}
