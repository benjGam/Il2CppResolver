namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Identifies the native IL2CPP type category reported by <c>il2cpp_type_get_type</c>.
/// This enum is internal because public APIs expose semantic type information rather than IL2CPP implementation constants.
/// </summary>
internal enum Il2CppTypeCode
{
    /// <summary>Represents the end-of-list marker.</summary>
    End = 0x00,
    /// <summary>Represents <c>System.Void</c>.</summary>
    Void = 0x01,
    /// <summary>Represents <c>System.Boolean</c>.</summary>
    Boolean = 0x02,
    /// <summary>Represents <c>System.Char</c>.</summary>
    Char = 0x03,
    /// <summary>Represents a signed 8-bit integer.</summary>
    I1 = 0x04,
    /// <summary>Represents an unsigned 8-bit integer.</summary>
    U1 = 0x05,
    /// <summary>Represents a signed 16-bit integer.</summary>
    I2 = 0x06,
    /// <summary>Represents an unsigned 16-bit integer.</summary>
    U2 = 0x07,
    /// <summary>Represents a signed 32-bit integer.</summary>
    I4 = 0x08,
    /// <summary>Represents an unsigned 32-bit integer.</summary>
    U4 = 0x09,
    /// <summary>Represents a signed 64-bit integer.</summary>
    I8 = 0x0A,
    /// <summary>Represents an unsigned 64-bit integer.</summary>
    U8 = 0x0B,
    /// <summary>Represents a 32-bit floating-point value.</summary>
    R4 = 0x0C,
    /// <summary>Represents a 64-bit floating-point value.</summary>
    R8 = 0x0D,
    /// <summary>Represents <c>System.String</c>.</summary>
    String = 0x0E,
    /// <summary>Represents an unmanaged pointer type.</summary>
    Pointer = 0x0F,
    /// <summary>Represents a managed by-reference type.</summary>
    ByReference = 0x10,
    /// <summary>Represents a managed value type.</summary>
    ValueType = 0x11,
    /// <summary>Represents a managed reference class.</summary>
    Class = 0x12,
    /// <summary>Represents a generic type parameter.</summary>
    GenericTypeParameter = 0x13,
    /// <summary>Represents a multidimensional managed array.</summary>
    Array = 0x14,
    /// <summary>Represents an instantiated generic type.</summary>
    GenericInstance = 0x15,
    /// <summary>Represents a typed reference.</summary>
    TypedByReference = 0x16,
    /// <summary>Represents a native signed integer.</summary>
    NativeInt = 0x18,
    /// <summary>Represents a native unsigned integer.</summary>
    NativeUInt = 0x19,
    /// <summary>Represents a function pointer.</summary>
    FunctionPointer = 0x1B,
    /// <summary>Represents <c>System.Object</c>.</summary>
    Object = 0x1C,
    /// <summary>Represents a single-dimensional zero-based managed array.</summary>
    SzArray = 0x1D,
    /// <summary>Represents a generic method parameter.</summary>
    GenericMethodParameter = 0x1E,
    /// <summary>Represents an IL2CPP internal type category.</summary>
    Internal = 0x21,
    /// <summary>Represents an enum marker used by some IL2CPP metadata paths.</summary>
    Enum = 0x55
}
