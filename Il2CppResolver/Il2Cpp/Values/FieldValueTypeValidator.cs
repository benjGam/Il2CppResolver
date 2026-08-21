using RuntimeTypeCode = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeCode;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Values;

/// <summary>
/// Validates that a requested unmanaged managed type can safely represent the exact IL2CPP field type discovered at runtime.
/// The validator intentionally supports only explicit scalar, native-integer, pointer and enum categories so arbitrary unmanaged structures cannot be read through accidental size compatibility.
/// </summary>
internal static class FieldValueTypeValidator
{
    /// <summary>Validates one non-enum scalar read and returns its exact storage size.</summary>
    /// <typeparam name="T">The unmanaged managed type requested by the consumer.</typeparam>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the field.</param>
    /// <returns>The exact number of bytes that must be readable for the value.</returns>
    public static int ValidateScalar<T>(RuntimeTypeDescriptor descriptor) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Type managedType = typeof(T);

        if (managedType.IsEnum)
            throw new InvalidOperationException($"Field '{descriptor.TypeName}' must be read with ReadEnum/ReadStaticEnum when an enum managed type is requested.");

        (RuntimeTypeCode expectedCode, int size) = GetManagedScalarType(managedType);

        if (descriptor.IsEnum)
            throw new InvalidOperationException($"Field type '{descriptor.TypeName}' is an enum and must be read through the enum-specific API.");

        bool compatible = expectedCode switch
        {
            RuntimeTypeCode.NativeInt => descriptor.TypeCode is RuntimeTypeCode.NativeInt or RuntimeTypeCode.Pointer or RuntimeTypeCode.FunctionPointer,
            RuntimeTypeCode.NativeUInt => descriptor.TypeCode is RuntimeTypeCode.NativeUInt or RuntimeTypeCode.Pointer or RuntimeTypeCode.FunctionPointer,
            _ => descriptor.TypeCode == expectedCode
        };

        if (!compatible)
            throw new InvalidOperationException($"Managed read type '{managedType.FullName}' is incompatible with IL2CPP field type '{descriptor.TypeName}' ({descriptor.TypeCode}).");

        return size;
    }

    /// <summary>Validates one enum read and returns the exact enum storage size.</summary>
    /// <typeparam name="TEnum">The unmanaged enum type requested by the consumer.</typeparam>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the field.</param>
    /// <returns>The exact number of bytes occupied by the enum underlying value.</returns>
    public static int ValidateEnum<TEnum>(RuntimeTypeDescriptor descriptor) where TEnum : unmanaged, Enum
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!descriptor.IsEnum || descriptor.EnumUnderlyingTypeCode is null)
            throw new InvalidOperationException($"IL2CPP field type '{descriptor.TypeName}' is not an enum.");

        Type enumType = typeof(TEnum);
        Type underlyingType = Enum.GetUnderlyingType(enumType);
        (RuntimeTypeCode expectedCode, int size) = GetManagedScalarType(underlyingType);

        if (descriptor.EnumUnderlyingTypeCode.Value != expectedCode)
            throw new InvalidOperationException($"Enum '{enumType.FullName}' uses underlying type '{underlyingType.FullName}', which is incompatible with IL2CPP enum '{descriptor.TypeName}' underlying type {descriptor.EnumUnderlyingTypeCode.Value}.");

        string? managedName = enumType.FullName;

        if (!string.IsNullOrWhiteSpace(managedName) && !NamesMatch(managedName, descriptor.TypeName))
            throw new InvalidOperationException($"Managed enum '{managedName}' does not match IL2CPP field enum '{descriptor.TypeName}'.");

        return size;
    }

    /// <summary>Validates that the runtime field type represents a managed reference and returns pointer-sized storage length.</summary>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the field.</param>
    /// <returns>The pointer-sized number of bytes occupied by the managed reference.</returns>
    public static int ValidateManagedReference(RuntimeTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.IsValueType || descriptor.IsEnum)
            throw new InvalidOperationException($"IL2CPP field type '{descriptor.TypeName}' is a value type and cannot be read as a managed object reference.");

        bool isReference = descriptor.TypeCode is RuntimeTypeCode.String or
                           RuntimeTypeCode.Class or
                           RuntimeTypeCode.Object or
                           RuntimeTypeCode.Array or
                           RuntimeTypeCode.SzArray or
                           RuntimeTypeCode.GenericInstance;

        if (!isReference)
            throw new InvalidOperationException($"IL2CPP field type '{descriptor.TypeName}' ({descriptor.TypeCode}) is not a supported managed reference category.");

        return IntPtr.Size;
    }

    /// <summary>Maps one explicitly supported managed scalar type to its IL2CPP category and exact storage size.</summary>
    /// <param name="managedType">The managed scalar type to map.</param>
    /// <returns>The expected IL2CPP type code and exact byte size.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type is not part of the conservative scalar-reading surface.</exception>
    private static (RuntimeTypeCode TypeCode, int Size) GetManagedScalarType(Type managedType)
    {
        if (managedType == typeof(bool)) return (RuntimeTypeCode.Boolean, 1);
        if (managedType == typeof(char)) return (RuntimeTypeCode.Char, 2);
        if (managedType == typeof(sbyte)) return (RuntimeTypeCode.I1, 1);
        if (managedType == typeof(byte)) return (RuntimeTypeCode.U1, 1);
        if (managedType == typeof(short)) return (RuntimeTypeCode.I2, 2);
        if (managedType == typeof(ushort)) return (RuntimeTypeCode.U2, 2);
        if (managedType == typeof(int)) return (RuntimeTypeCode.I4, 4);
        if (managedType == typeof(uint)) return (RuntimeTypeCode.U4, 4);
        if (managedType == typeof(long)) return (RuntimeTypeCode.I8, 8);
        if (managedType == typeof(ulong)) return (RuntimeTypeCode.U8, 8);
        if (managedType == typeof(float)) return (RuntimeTypeCode.R4, 4);
        if (managedType == typeof(double)) return (RuntimeTypeCode.R8, 8);
        if (managedType == typeof(nint)) return (RuntimeTypeCode.NativeInt, IntPtr.Size);
        if (managedType == typeof(nuint)) return (RuntimeTypeCode.NativeUInt, IntPtr.Size);

        throw new NotSupportedException($"Managed type '{managedType.FullName}' is not supported by the scalar field-value reader.");
    }

    /// <summary>Compares managed and IL2CPP enum names while normalizing common nested-type separators.</summary>
    /// <param name="managedName">The managed runtime enum name.</param>
    /// <param name="il2CppName">The IL2CPP semantic enum name.</param>
    /// <returns><see langword="true"/> when both names identify the same semantic type.</returns>
    private static bool NamesMatch(string managedName, string il2CppName)
    {
        string normalizedManaged = managedName.Replace('+', '.').Replace('/', '.');
        string normalizedIl2Cpp = il2CppName.Replace('+', '.').Replace('/', '.');
        return string.Equals(normalizedManaged, normalizedIl2Cpp, StringComparison.Ordinal);
    }
}
