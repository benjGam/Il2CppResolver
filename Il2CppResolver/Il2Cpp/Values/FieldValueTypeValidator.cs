using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeTypeCode = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeCode;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Values;

/// <summary>
/// Validates that a requested managed representation can safely model the exact IL2CPP runtime type discovered for a field or array element.
/// The validator keeps scalar, enum, reference, string, array and blittable-structure paths explicit so neither equal byte size nor the broad <c>unmanaged</c> constraint can silently authorize an incompatible interpretation.
/// </summary>
internal static class FieldValueTypeValidator
{
    /// <summary>Validates one non-enum scalar read and returns its exact storage size.</summary>
    /// <typeparam name="T">The unmanaged managed type requested by the consumer.</typeparam>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <returns>The exact number of bytes that must be readable for the value.</returns>
    public static int ValidateScalar<T>(RuntimeTypeDescriptor descriptor) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Type managedType = typeof(T);

        if (managedType.IsEnum)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' must be read with the enum-specific API when an enum managed type is requested.");

        (RuntimeTypeCode expectedCode, int size) = GetManagedScalarType(managedType);

        if (descriptor.IsEnum)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' is an enum and must be read through the enum-specific API.");

        bool compatible = expectedCode switch
        {
            RuntimeTypeCode.NativeInt => descriptor.TypeCode is RuntimeTypeCode.NativeInt or RuntimeTypeCode.Pointer or RuntimeTypeCode.FunctionPointer,
            RuntimeTypeCode.NativeUInt => descriptor.TypeCode is RuntimeTypeCode.NativeUInt or RuntimeTypeCode.Pointer or RuntimeTypeCode.FunctionPointer,
            _ => descriptor.TypeCode == expectedCode
        };

        if (!compatible)
            throw new InvalidOperationException($"Managed read type '{managedType.FullName}' is incompatible with IL2CPP type '{descriptor.TypeName}' ({descriptor.TypeCode}).");

        return size;
    }

    /// <summary>Validates one enum read and returns the exact enum storage size.</summary>
    /// <typeparam name="TEnum">The unmanaged enum type requested by the consumer.</typeparam>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <returns>The exact number of bytes occupied by the enum underlying value.</returns>
    public static int ValidateEnum<TEnum>(RuntimeTypeDescriptor descriptor) where TEnum : unmanaged, Enum
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!descriptor.IsEnum || descriptor.EnumUnderlyingTypeCode is null)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' is not an enum.");

        Type enumType = typeof(TEnum);
        Type underlyingType = Enum.GetUnderlyingType(enumType);
        (RuntimeTypeCode expectedCode, int size) = GetManagedScalarType(underlyingType);

        if (descriptor.EnumUnderlyingTypeCode.Value != expectedCode)
            throw new InvalidOperationException($"Enum '{enumType.FullName}' uses underlying type '{underlyingType.FullName}', which is incompatible with IL2CPP enum '{descriptor.TypeName}' underlying type {descriptor.EnumUnderlyingTypeCode.Value}.");

        string? managedName = enumType.FullName;

        if (!string.IsNullOrWhiteSpace(managedName) && !NamesMatch(managedName, descriptor.TypeName))
            throw new InvalidOperationException($"Managed enum '{managedName}' does not match IL2CPP enum '{descriptor.TypeName}'.");

        return size;
    }

    /// <summary>Validates that the runtime type represents a supported managed reference and returns pointer-sized storage length.</summary>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <returns>The pointer-sized number of bytes occupied by the managed reference.</returns>
    public static int ValidateManagedReference(RuntimeTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.IsValueType || descriptor.IsEnum)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' is a value type and cannot be read as a managed object reference.");

        bool isReference = descriptor.TypeCode is RuntimeTypeCode.String or
                           RuntimeTypeCode.Class or
                           RuntimeTypeCode.Object or
                           RuntimeTypeCode.Array or
                           RuntimeTypeCode.SzArray or
                           RuntimeTypeCode.GenericInstance;

        if (!isReference)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' ({descriptor.TypeCode}) is not a supported managed reference category.");

        return IntPtr.Size;
    }

    /// <summary>Validates that the runtime type is exactly <c>System.String</c> and returns pointer-sized storage length.</summary>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <returns>The pointer-sized number of bytes occupied by the string reference.</returns>
    public static int ValidateString(RuntimeTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.TypeCode != RuntimeTypeCode.String || descriptor.IsValueType || descriptor.IsEnum)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' ({descriptor.TypeCode}) is not System.String.");

        return IntPtr.Size;
    }

    /// <summary>Validates that the runtime type is a single-dimensional zero-based managed array and returns pointer-sized storage length.</summary>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <returns>The pointer-sized number of bytes occupied by the array reference.</returns>
    public static int ValidateVectorArray(RuntimeTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.TypeCode == RuntimeTypeCode.Array)
            throw new NotSupportedException($"Multidimensional IL2CPP array type '{descriptor.TypeName}' is intentionally unsupported in this version.");

        if (descriptor.TypeCode != RuntimeTypeCode.SzArray || descriptor.IsValueType || descriptor.IsEnum)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' ({descriptor.TypeCode}) is not a single-dimensional zero-based managed array.");

        if (descriptor.ClassAddress == 0)
            throw new InvalidDataException($"IL2CPP array type '{descriptor.TypeName}' does not expose a runtime class identity.");

        return IntPtr.Size;
    }

    /// <summary>Validates an explicitly requested blittable value-type read and returns the exact native value size.</summary>
    /// <typeparam name="T">The unmanaged managed structure requested by the consumer.</typeparam>
    /// <param name="descriptor">The runtime IL2CPP type descriptor associated with the value.</param>
    /// <param name="metadata">The complete runtime metadata for the value-type class.</param>
    /// <returns>The exact native byte size required for the blittable value.</returns>
    public static unsafe int ValidateBlittable<T>(RuntimeTypeDescriptor descriptor, RuntimeClassMetadata metadata) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(metadata);
        Type managedType = typeof(T);

        if (managedType.IsEnum || managedType.IsPrimitive || managedType == typeof(nint) || managedType == typeof(nuint))
            throw new InvalidOperationException($"Managed type '{managedType.FullName}' must use the scalar or enum reading API rather than ReadBlittable.");

        if (!descriptor.IsValueType || descriptor.IsEnum || descriptor.ClassAddress == 0)
            throw new InvalidOperationException($"IL2CPP type '{descriptor.TypeName}' is not a non-enum value type suitable for blittable reading.");

        if (metadata.ClassAddress != descriptor.ClassAddress)
            throw new InvalidDataException("The supplied class metadata does not belong to the requested IL2CPP value type.");

        if (!metadata.IsValueType || metadata.IsEnum || !metadata.IsBlittable)
            throw new NotSupportedException($"IL2CPP value type '{descriptor.TypeName}' is not reported as blittable by the runtime.");

        if (metadata.ValueSize is null || metadata.ValueSize.Value <= 0)
            throw new InvalidDataException($"IL2CPP value type '{descriptor.TypeName}' does not expose a valid native value size.");

        int managedSize = sizeof(T);

        if (managedSize != metadata.ValueSize.Value)
            throw new InvalidOperationException($"Managed blittable type '{managedType.FullName}' occupies {managedSize} byte(s), while IL2CPP type '{descriptor.TypeName}' occupies {metadata.ValueSize.Value} byte(s).");

        string? managedName = managedType.FullName;

        if (string.IsNullOrWhiteSpace(managedName) || !NamesMatch(managedName, descriptor.TypeName))
            throw new InvalidOperationException($"Managed blittable type '{managedName ?? managedType.Name}' does not match IL2CPP value type '{descriptor.TypeName}'.");

        return managedSize;
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
        throw new NotSupportedException($"Managed type '{managedType.FullName}' is not supported by the scalar value reader.");
    }

    /// <summary>Compares managed and IL2CPP semantic type names while normalizing common nested-type separators.</summary>
    /// <param name="managedName">The managed runtime type name.</param>
    /// <param name="il2CppName">The IL2CPP semantic type name.</param>
    /// <returns><see langword="true"/> when both names identify the same semantic type.</returns>
    private static bool NamesMatch(string managedName, string il2CppName)
    {
        string normalizedManaged = managedName.Replace('+', '.').Replace('/', '.');
        string normalizedIl2Cpp = il2CppName.Replace('+', '.').Replace('/', '.');
        return string.Equals(normalizedManaged, normalizedIl2Cpp, StringComparison.Ordinal);
    }
}
