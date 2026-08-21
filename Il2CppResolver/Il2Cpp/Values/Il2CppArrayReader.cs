using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using UnityIl2CppResolver.Il2Cpp.Values.Model;
using UnityIl2CppResolver.Native.Memory;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Values;

/// <summary>
/// Materializes and reads live single-dimensional zero-based IL2CPP arrays through runtime-reported length, byte-length, element-class and element-size metadata.
/// The reader supports scalar, enum, reference, string and explicitly validated blittable elements while rejecting multidimensional arrays and validating every index and complete remote memory range.
/// </summary>
internal sealed class Il2CppArrayReader
{
    /// <summary>
    /// Defines the vector payload offset of the stable Windows x64 <c>Il2CppArray</c> object header: two pointers for <c>Il2CppObject</c>, one bounds pointer and one pointer-sized maximum length.
    /// Runtime-reported byte length and element size are cross-validated before this structural invariant is used for target-memory access.
    /// </summary>
    private const int VectorDataOffsetX64 = 0x20;
    /// <summary>Provides safe read-only access to the target process address space.</summary>
    private readonly ProcessMemory _memory;
    /// <summary>Provides public IL2CPP array APIs for instance-specific length validation.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Provides cached runtime type descriptors, array element types and array element sizes.</summary>
    private readonly Il2CppTypeCatalog _types;
    /// <summary>Decodes string references stored inside managed arrays.</summary>
    private readonly Il2CppStringReader _strings;
    /// <summary>Defines the timeout applied to individual runtime calls.</summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>Initializes an array reader for one resolver session.</summary>
    /// <param name="memory">The read-only target process memory accessor.</param>
    /// <param name="runtime">The live IL2CPP runtime facade.</param>
    /// <param name="types">The session-scoped type catalogue.</param>
    /// <param name="strings">The managed-string reader reused for string elements.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppArrayReader(ProcessMemory memory, Il2CppRuntime runtime, Il2CppTypeCatalog types, Il2CppStringReader strings, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(strings);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _memory = memory;
        _runtime = runtime;
        _types = types;
        _strings = strings;
        _callTimeout = callTimeout;
    }

    /// <summary>Creates one validated session-bound array result from a non-null array reference and declared array type.</summary>
    /// <param name="arrayAddress">The remote <c>Il2CppArray*</c> identity.</param>
    /// <param name="arrayTypeAddress">The declared array <c>Il2CppType*</c> identity.</param>
    /// <param name="navigator">The owning session navigator.</param>
    /// <param name="generation">The cache generation that produced the array reference.</param>
    /// <returns>The validated session-bound array result.</returns>
    public ResolvedArray Resolve(nint arrayAddress, nint arrayTypeAddress, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        Il2CppArrayDescriptor descriptor = Inspect(arrayAddress, arrayTypeAddress);
        return new ResolvedArray(descriptor.ArrayAddress, descriptor.Length, descriptor.ElementTypeName, descriptor.DataAddress, descriptor.ElementTypeAddress, descriptor.ElementSize, navigator, generation);
    }

    /// <summary>Reads one explicitly supported scalar array element.</summary>
    /// <typeparam name="T">The exact supported scalar type.</typeparam>
    /// <param name="array">The validated session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The validated scalar value.</returns>
    public T Read<T>(ResolvedArray array, int index) where T : unmanaged
    {
        RuntimeTypeDescriptor descriptor = GetElementDescriptor(array);
        int size = FieldValueTypeValidator.ValidateScalar<T>(descriptor);
        ValidateElementSize(array, size);
        nint address = GetElementAddress(array, index, size);
        return _memory.Read<T>(address);
    }

    /// <summary>Reads one array element as the exact requested enum type.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="array">The validated session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The validated enum value.</returns>
    public TEnum ReadEnum<TEnum>(ResolvedArray array, int index) where TEnum : unmanaged, Enum
    {
        RuntimeTypeDescriptor descriptor = GetElementDescriptor(array);
        int size = FieldValueTypeValidator.ValidateEnum<TEnum>(descriptor);
        ValidateElementSize(array, size);
        nint address = GetElementAddress(array, index, size);
        return _memory.Read<TEnum>(address);
    }

    /// <summary>Reads one array element as a managed object reference.</summary>
    /// <param name="array">The validated session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero.</returns>
    public nint ReadReference(ResolvedArray array, int index)
    {
        RuntimeTypeDescriptor descriptor = GetElementDescriptor(array);
        int size = FieldValueTypeValidator.ValidateManagedReference(descriptor);
        ValidateElementSize(array, size);
        nint address = GetElementAddress(array, index, size);
        return _memory.ReadPointer(address);
    }

    /// <summary>Reads one array element as a managed <c>System.String</c>.</summary>
    /// <param name="array">The validated session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadString(ResolvedArray array, int index)
    {
        RuntimeTypeDescriptor descriptor = GetElementDescriptor(array);
        int size = FieldValueTypeValidator.ValidateString(descriptor);
        ValidateElementSize(array, size);
        nint address = GetElementAddress(array, index, size);
        return _strings.Read(_memory.ReadPointer(address));
    }

    /// <summary>Reads one array element as an explicitly validated blittable value type.</summary>
    /// <typeparam name="T">The unmanaged managed structure matching the IL2CPP element type.</typeparam>
    /// <param name="array">The validated session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadBlittable<T>(ResolvedArray array, int index) where T : unmanaged
    {
        RuntimeTypeDescriptor descriptor = GetElementDescriptor(array);

        if (descriptor.ClassAddress == 0)
            throw new InvalidDataException($"IL2CPP array element type '{descriptor.TypeName}' does not expose a runtime class identity.");

        RuntimeClassMetadata metadata = _types.GetClassMetadata(descriptor.ClassAddress);
        int size = FieldValueTypeValidator.ValidateBlittable<T>(descriptor, metadata);
        ValidateElementSize(array, size);
        nint address = GetElementAddress(array, index, size);
        return _memory.Read<T>(address);
    }

    /// <summary>Inspects one live vector array and cross-validates runtime-reported length, byte length and element size.</summary>
    /// <param name="arrayAddress">The non-null remote array identity.</param>
    /// <param name="arrayTypeAddress">The declared array runtime type identity.</param>
    /// <returns>The complete validated array descriptor.</returns>
    private Il2CppArrayDescriptor Inspect(nint arrayAddress, nint arrayTypeAddress)
    {
        if (arrayAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(arrayAddress), "The IL2CPP array address cannot be zero.");

        RuntimeTypeDescriptor arrayType = _types.GetTypeDescriptor(arrayTypeAddress);
        FieldValueTypeValidator.ValidateVectorArray(arrayType);
        uint rawLength = _runtime.GetArrayLength(arrayAddress, _callTimeout);

        if (rawLength > int.MaxValue)
            throw new InvalidDataException($"IL2CPP array at 0x{arrayAddress:X} contains {rawLength} element(s), exceeding the supported local index range.");

        int elementSize = _types.GetArrayElementSize(arrayType.ClassAddress);
        uint byteLength = _runtime.GetArrayByteLength(arrayAddress, _callTimeout);
        ulong expectedByteLength = checked((ulong)rawLength * (uint)elementSize);

        if (expectedByteLength != byteLength)
            throw new InvalidDataException($"IL2CPP array at 0x{arrayAddress:X} reports byte length 0x{byteLength:X}, but length {rawLength} and element size {elementSize} require 0x{expectedByteLength:X} byte(s).");

        nint elementTypeAddress = _types.GetArrayElementTypeAddress(arrayType.ClassAddress);
        RuntimeTypeDescriptor elementType = _types.GetTypeDescriptor(elementTypeAddress);
        nint dataAddress = AddOffset(arrayAddress, VectorDataOffsetX64, "IL2CPP array vector payload");

        if (byteLength > 0 && !_memory.IsReadableRange(dataAddress, byteLength))
            throw new InvalidDataException($"IL2CPP array payload at 0x{dataAddress:X} with size 0x{byteLength:X} is not completely readable.");

        return new Il2CppArrayDescriptor(arrayAddress, checked((int)rawLength), elementSize, dataAddress, elementTypeAddress, elementType.TypeName);
    }

    /// <summary>Gets and validates the runtime element descriptor owned by one array result.</summary>
    /// <param name="array">The array whose element type should be inspected.</param>
    /// <returns>The cached runtime element type descriptor.</returns>
    private RuntimeTypeDescriptor GetElementDescriptor(ResolvedArray array)
    {
        ArgumentNullException.ThrowIfNull(array);
        return _types.GetTypeDescriptor(array.RuntimeElementTypeAddress);
    }

    /// <summary>Validates that the semantic element representation consumes exactly the runtime-reported array slot size.</summary>
    /// <param name="array">The array whose element slot should be validated.</param>
    /// <param name="requestedSize">The exact byte size required by the requested element representation.</param>
    private static void ValidateElementSize(ResolvedArray array, int requestedSize)
    {
        if (requestedSize != array.ElementSize)
            throw new InvalidOperationException($"Requested element representation requires {requestedSize} byte(s), while IL2CPP array '{array.ElementTypeName}[]' uses {array.ElementSize}-byte element slots.");
    }

    /// <summary>Validates one index and calculates the readable remote element address.</summary>
    /// <param name="array">The array containing the requested element.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="size">The exact number of bytes that will be read.</param>
    /// <returns>The validated remote element address.</returns>
    private nint GetElementAddress(ResolvedArray array, int index, int size)
    {
        ArgumentNullException.ThrowIfNull(array);

        if ((uint)index >= (uint)array.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Array index must be between 0 and {array.Length - 1}.");

        nuint offset = checked((nuint)index * (nuint)array.ElementSize);
        nint address = AddOffset(array.DataAddress, offset, "IL2CPP array element");

        if (!_memory.IsReadableRange(address, checked((nuint)size)))
            throw new InvalidDataException($"IL2CPP array element range at 0x{address:X} with size 0x{size:X} is not completely readable.");

        return address;
    }

    /// <summary>Adds one byte offset to a remote address while rejecting overflow and unsupported signed native-address results.</summary>
    /// <param name="baseAddress">The remote base address.</param>
    /// <param name="offset">The byte offset to add.</param>
    /// <param name="description">The diagnostic calculation description.</param>
    /// <returns>The calculated remote address.</returns>
    private static nint AddOffset(nint baseAddress, nuint offset, string description)
    {
        ulong baseValue = unchecked((ulong)(nuint)baseAddress);
        ulong offsetValue = (ulong)offset;

        if (offsetValue > ulong.MaxValue - baseValue)
            throw new InvalidDataException($"Address calculation for {description} overflowed from base 0x{baseAddress:X} with offset 0x{offset:X}.");

        ulong result = baseValue + offsetValue;

        if (result > long.MaxValue)
            throw new InvalidDataException($"Address calculation for {description} produced unsupported native address 0x{result:X}.");

        return (nint)(long)result;
    }
}
