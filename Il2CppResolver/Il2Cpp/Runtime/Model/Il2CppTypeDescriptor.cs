namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes one live <c>Il2CppType*</c> together with the runtime class and enum information required for safe field-value interpretation.
/// This immutable model is cached by native type identity for one resolver generation.
/// </summary>
internal sealed class Il2CppTypeDescriptor
{
    /// <summary>Gets the native <c>Il2CppType*</c> address represented by this descriptor.</summary>
    public nint TypeAddress { get; }
    /// <summary>Gets the IL2CPP type category reported by the runtime.</summary>
    public Il2CppTypeCode TypeCode { get; }
    /// <summary>Gets the semantic managed type name.</summary>
    public string TypeName { get; }
    /// <summary>Gets the associated <c>Il2CppClass*</c> address when the runtime can provide one; otherwise zero.</summary>
    public nint ClassAddress { get; }
    /// <summary>Gets a value indicating whether the associated runtime class is a managed value type.</summary>
    public bool IsValueType { get; }
    /// <summary>Gets a value indicating whether the associated runtime class is an enum.</summary>
    public bool IsEnum { get; }
    /// <summary>Gets the enum underlying IL2CPP scalar type when <see cref="IsEnum"/> is <see langword="true"/>; otherwise <see langword="null"/>.</summary>
    public Il2CppTypeCode? EnumUnderlyingTypeCode { get; }

    /// <summary>Initializes an immutable IL2CPP type descriptor.</summary>
    /// <param name="typeAddress">The native <c>Il2CppType*</c> address.</param>
    /// <param name="typeCode">The IL2CPP runtime type category.</param>
    /// <param name="typeName">The semantic managed type name.</param>
    /// <param name="classAddress">The associated <c>Il2CppClass*</c> address, or zero when unavailable.</param>
    /// <param name="isValueType">Whether the associated class is a value type.</param>
    /// <param name="isEnum">Whether the associated class is an enum.</param>
    /// <param name="enumUnderlyingTypeCode">The enum underlying scalar type when applicable.</param>
    public Il2CppTypeDescriptor(nint typeAddress, Il2CppTypeCode typeCode, string typeName, nint classAddress, bool isValueType, bool isEnum, Il2CppTypeCode? enumUnderlyingTypeCode)
    {
        if (typeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(typeAddress), "The IL2CPP type address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (isEnum && enumUnderlyingTypeCode is null)
            throw new ArgumentException("An enum type descriptor must expose its underlying scalar type.", nameof(enumUnderlyingTypeCode));

        if (!isEnum && enumUnderlyingTypeCode is not null)
            throw new ArgumentException("Only enum type descriptors can expose an underlying scalar type.", nameof(enumUnderlyingTypeCode));

        TypeAddress = typeAddress;
        TypeCode = typeCode;
        TypeName = typeName;
        ClassAddress = classAddress;
        IsValueType = isValueType;
        IsEnum = isEnum;
        EnumUnderlyingTypeCode = enumUnderlyingTypeCode;
    }
}
