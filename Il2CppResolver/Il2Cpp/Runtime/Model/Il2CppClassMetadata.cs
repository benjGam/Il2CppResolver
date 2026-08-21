using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Stores runtime metadata obtained for one live <c>Il2CppClass*</c> through public IL2CPP inspection APIs.
/// This internal representation is converted into the public <c>ResolvedTypeMetadata</c> result by the resolution session.
/// </summary>
internal sealed class Il2CppClassMetadata
{
    /// <summary>Gets the native class identity described by this metadata.</summary>
    public nint ClassAddress { get; }
    /// <summary>Gets the managed type attributes reported by IL2CPP.</summary>
    public TypeAttributes Attributes { get; }
    /// <summary>Gets the metadata token associated with the class.</summary>
    public uint MetadataToken { get; }
    /// <summary>Gets a value indicating whether the class is a value type.</summary>
    public bool IsValueType { get; }
    /// <summary>Gets a value indicating whether the class is an enum.</summary>
    public bool IsEnum { get; }
    /// <summary>Gets a value indicating whether the class is blittable according to IL2CPP.</summary>
    public bool IsBlittable { get; }
    /// <summary>Gets a value indicating whether the class is a generic type definition.</summary>
    public bool IsGeneric { get; }
    /// <summary>Gets a value indicating whether the class represents an inflated generic instantiation.</summary>
    public bool IsInflated { get; }
    /// <summary>Gets the native value size for value types; otherwise <see langword="null"/>.</summary>
    public int? ValueSize { get; }
    /// <summary>Gets the native alignment reported with <see cref="ValueSize"/>; otherwise <see langword="null"/>.</summary>
    public uint? ValueAlignment { get; }

    /// <summary>Initializes immutable class metadata.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> address.</param>
    /// <param name="attributes">The managed type attributes.</param>
    /// <param name="metadataToken">The metadata token associated with the type.</param>
    /// <param name="isValueType">Whether the class is a value type.</param>
    /// <param name="isEnum">Whether the class is an enum.</param>
    /// <param name="isBlittable">Whether IL2CPP reports the class as blittable.</param>
    /// <param name="isGeneric">Whether the class is a generic type definition.</param>
    /// <param name="isInflated">Whether the class is an inflated generic type.</param>
    /// <param name="valueSize">The value size when applicable.</param>
    /// <param name="valueAlignment">The value alignment when applicable.</param>
    public Il2CppClassMetadata(nint classAddress, TypeAttributes attributes, uint metadataToken, bool isValueType, bool isEnum, bool isBlittable, bool isGeneric, bool isInflated, int? valueSize, uint? valueAlignment)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class address cannot be zero.");

        if (isValueType && (valueSize is null || valueAlignment is null))
            throw new ArgumentException("Value types must expose both native size and alignment metadata.");

        if (!isValueType && (valueSize is not null || valueAlignment is not null))
            throw new ArgumentException("Reference types cannot expose value-type size metadata.");

        ClassAddress = classAddress;
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
