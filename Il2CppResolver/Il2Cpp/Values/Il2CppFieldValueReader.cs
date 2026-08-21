using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using UnityIl2CppResolver.Native.Memory;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Values;

/// <summary>
/// Reads explicitly supported field values from validated IL2CPP storage locations.
/// The reader validates storage kind, runtime type, static or instance bounds and complete memory readability before interpreting scalars, enums, references, strings, arrays or explicitly validated blittable value types.
/// </summary>
internal sealed class Il2CppFieldValueReader
{
    /// <summary>Provides safe read-only access to the target process address space.</summary>
    private readonly ProcessMemory _memory;
    /// <summary>Provides cached runtime type descriptors, metadata and instance-size information.</summary>
    private readonly Il2CppTypeCatalog _types;
    /// <summary>Decodes validated managed string references.</summary>
    private readonly Il2CppStringReader _strings;

    /// <summary>Initializes a field-value reader over the specified process memory and runtime type catalogue.</summary>
    /// <param name="memory">The read-only target process memory accessor.</param>
    /// <param name="types">The session-scoped type introspection catalogue.</param>
    /// <param name="strings">The managed-string reader reused by string field operations.</param>
    public Il2CppFieldValueReader(ProcessMemory memory, Il2CppTypeCatalog types, Il2CppStringReader strings)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(strings);
        _memory = memory;
        _types = types;
        _strings = strings;
    }

    /// <summary>Reads a supported unmanaged scalar from validated normal static-field storage.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the field.</typeparam>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The scalar value read from the target process.</returns>
    public T ReadStatic<T>(ResolvedField field, ResolvedFieldStorage storage) where T : unmanaged
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateScalar<T>(descriptor);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _memory.Read<T>(storage.StorageAddress);
    }

    /// <summary>Reads a managed object reference from validated normal static-field storage.</summary>
    /// <param name="field">The resolved normal static reference field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero for a null managed reference.</returns>
    public nint ReadStaticReference(ResolvedField field, ResolvedFieldStorage storage)
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateManagedReference(descriptor);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _memory.ReadPointer(storage.StorageAddress);
    }

    /// <summary>Reads a managed <c>System.String</c> from validated normal static-field storage.</summary>
    /// <param name="field">The resolved normal static string field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadStaticString(ResolvedField field, ResolvedFieldStorage storage)
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateString(descriptor);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _strings.Read(_memory.ReadPointer(storage.StorageAddress));
    }

    /// <summary>Reads a single-dimensional zero-based managed array reference from validated normal static-field storage.</summary>
    /// <param name="field">The resolved normal static array field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The remote <c>Il2CppArray*</c> address, or zero for a null array reference.</returns>
    public nint ReadStaticArrayReference(ResolvedField field, ResolvedFieldStorage storage)
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateVectorArray(descriptor);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _memory.ReadPointer(storage.StorageAddress);
    }

    /// <summary>Reads an enum value from validated normal static-field storage.</summary>
    /// <typeparam name="TEnum">The exact managed enum type expected by the field.</typeparam>
    /// <param name="field">The resolved normal static enum field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The enum value read from the target process.</returns>
    public TEnum ReadStaticEnum<TEnum>(ResolvedField field, ResolvedFieldStorage storage) where TEnum : unmanaged, Enum
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateEnum<TEnum>(descriptor);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _memory.Read<TEnum>(storage.StorageAddress);
    }

    /// <summary>Reads one explicitly validated blittable value type from normal static-field storage.</summary>
    /// <typeparam name="T">The unmanaged managed structure matching the IL2CPP field type.</typeparam>
    /// <param name="field">The resolved normal static value-type field.</param>
    /// <param name="storage">The validated concrete static-field storage mapping.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadStaticBlittable<T>(ResolvedField field, ResolvedFieldStorage storage) where T : unmanaged
    {
        ValidateStaticField(field, storage);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);

        if (descriptor.ClassAddress == 0)
            throw new InvalidDataException($"IL2CPP field type '{descriptor.TypeName}' does not expose a runtime class identity.");

        RuntimeClassMetadata metadata = _types.GetClassMetadata(descriptor.ClassAddress);
        int size = FieldValueTypeValidator.ValidateBlittable<T>(descriptor, metadata);
        ValidateStaticBounds(storage, size);
        ValidateReadable(storage.StorageAddress, size);
        return _memory.Read<T>(storage.StorageAddress);
    }

    /// <summary>Reads a supported unmanaged scalar from one reference-type IL2CPP object instance.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the field.</typeparam>
    /// <param name="field">The resolved instance field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The scalar value read from the instance.</returns>
    public T ReadInstance<T>(ResolvedField field, nint instanceAddress) where T : unmanaged
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateScalar<T>(descriptor);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _memory.Read<T>(storageAddress);
    }

    /// <summary>Reads a managed object reference from one reference-type IL2CPP object instance.</summary>
    /// <param name="field">The resolved instance reference field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The referenced remote <c>Il2CppObject*</c> address, or zero for a null reference.</returns>
    public nint ReadInstanceReference(ResolvedField field, nint instanceAddress)
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateManagedReference(descriptor);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _memory.ReadPointer(storageAddress);
    }

    /// <summary>Reads a managed <c>System.String</c> from one reference-type IL2CPP object instance.</summary>
    /// <param name="field">The resolved instance string field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadInstanceString(ResolvedField field, nint instanceAddress)
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateString(descriptor);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _strings.Read(_memory.ReadPointer(storageAddress));
    }

    /// <summary>Reads a single-dimensional zero-based managed array reference from one reference-type IL2CPP object instance.</summary>
    /// <param name="field">The resolved instance array field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The remote <c>Il2CppArray*</c> address, or zero for a null array reference.</returns>
    public nint ReadInstanceArrayReference(ResolvedField field, nint instanceAddress)
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateVectorArray(descriptor);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _memory.ReadPointer(storageAddress);
    }

    /// <summary>Reads an enum value from one reference-type IL2CPP object instance.</summary>
    /// <typeparam name="TEnum">The exact managed enum type expected by the field.</typeparam>
    /// <param name="field">The resolved instance enum field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The enum value read from the instance.</returns>
    public TEnum ReadInstanceEnum<TEnum>(ResolvedField field, nint instanceAddress) where TEnum : unmanaged, Enum
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);
        int size = FieldValueTypeValidator.ValidateEnum<TEnum>(descriptor);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _memory.Read<TEnum>(storageAddress);
    }

    /// <summary>Reads one explicitly validated blittable value type from a reference-type IL2CPP object instance.</summary>
    /// <typeparam name="T">The unmanaged managed structure matching the IL2CPP field type.</typeparam>
    /// <param name="field">The resolved instance value-type field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> base address containing the field.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadInstanceBlittable<T>(ResolvedField field, nint instanceAddress) where T : unmanaged
    {
        ValidateInstanceField(field, instanceAddress);
        RuntimeTypeDescriptor descriptor = _types.GetTypeDescriptor(field.RuntimeTypeAddress);

        if (descriptor.ClassAddress == 0)
            throw new InvalidDataException($"IL2CPP field type '{descriptor.TypeName}' does not expose a runtime class identity.");

        RuntimeClassMetadata metadata = _types.GetClassMetadata(descriptor.ClassAddress);
        int size = FieldValueTypeValidator.ValidateBlittable<T>(descriptor, metadata);
        nint storageAddress = GetInstanceStorageAddress(field, instanceAddress, size);
        ValidateReadable(storageAddress, size);
        return _memory.Read<T>(storageAddress);
    }

    /// <summary>Validates that the requested field and mapping represent the same normal static storage identity.</summary>
    /// <param name="field">The resolved field to validate.</param>
    /// <param name="storage">The concrete field-storage mapping to validate.</param>
    private static void ValidateStaticField(ResolvedField field, ResolvedFieldStorage storage)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(storage);

        if (field.StorageKind == FieldStorageKind.ThreadStatic)
            throw new NotSupportedException($"Thread-static field '{field.Query.Name}' is not supported by the field-value reader.");

        if (field.StorageKind == FieldStorageKind.Literal)
            throw new NotSupportedException($"Literal field '{field.Query.Name}' does not expose ordinary runtime storage.");

        if (field.StorageKind != FieldStorageKind.Static)
            throw new InvalidOperationException($"Field '{field.Query.Name}' uses storage kind '{field.StorageKind}' and cannot be read through a static-field API.");

        if (storage.Field.FieldInfoAddress != field.FieldInfoAddress || storage.Field.DeclaringType.ClassAddress != field.DeclaringType.ClassAddress)
            throw new InvalidDataException("The supplied field storage mapping does not belong to the requested field identity.");
    }

    /// <summary>Validates that the requested field can be addressed relative to one reference-type object instance.</summary>
    /// <param name="field">The resolved field to validate.</param>
    /// <param name="instanceAddress">The remote object base address supplied by the caller.</param>
    private void ValidateInstanceField(ResolvedField field, nint instanceAddress)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (instanceAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(instanceAddress), "The IL2CPP object instance address cannot be zero.");

        if (field.StorageKind == FieldStorageKind.ThreadStatic)
            throw new NotSupportedException($"Thread-static field '{field.Query.Name}' is not supported by the field-value reader.");

        if (field.StorageKind == FieldStorageKind.Literal)
            throw new NotSupportedException($"Literal field '{field.Query.Name}' does not expose ordinary runtime storage.");

        if (field.StorageKind != FieldStorageKind.Instance)
            throw new InvalidOperationException($"Field '{field.Query.Name}' uses storage kind '{field.StorageKind}' and cannot be read relative to an object instance.");

        if (_types.IsClassValueType(field.DeclaringType.ClassAddress))
            throw new NotSupportedException($"Instance field reading from unboxed value type '{field.DeclaringType.Query.Namespace}.{field.DeclaringType.Query.Name}' is intentionally unsupported in this version.");
    }

    /// <summary>Calculates and validates the concrete storage address of an instance field.</summary>
    /// <param name="field">The resolved instance field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="valueSize">The exact number of bytes occupied by the requested value.</param>
    /// <returns>The concrete remote field storage address.</returns>
    private nint GetInstanceStorageAddress(ResolvedField field, nint instanceAddress, int valueSize)
    {
        if (field.InstanceOffset is not nuint offset)
            throw new InvalidDataException($"Instance field '{field.Query.Name}' does not expose an instance offset.");

        int instanceSize = _types.GetClassInstanceSize(field.DeclaringType.ClassAddress);
        ValidateRangeWithinContainer(offset, valueSize, checked((uint)instanceSize), $"instance of '{field.DeclaringType.Query.Namespace}.{field.DeclaringType.Query.Name}'");
        return AddOffset(instanceAddress, offset, $"instance field '{field.Query.Name}'");
    }

    /// <summary>Validates that a static field value fits completely inside the declaring class static-data block.</summary>
    /// <param name="storage">The resolved static field storage mapping.</param>
    /// <param name="valueSize">The exact requested value size.</param>
    private static void ValidateStaticBounds(ResolvedFieldStorage storage, int valueSize)
    {
        ValidateRangeWithinContainer(storage.StaticStorageOffset, valueSize, storage.StaticFieldsSize, $"static-fields block for '{storage.Field.DeclaringType.Query.Namespace}.{storage.Field.DeclaringType.Query.Name}'");
    }

    /// <summary>Validates that an offset plus value size belongs completely to one bounded runtime storage block.</summary>
    /// <param name="offset">The byte offset inside the storage container.</param>
    /// <param name="valueSize">The exact value size in bytes.</param>
    /// <param name="containerSize">The total storage container size in bytes.</param>
    /// <param name="containerDescription">The diagnostic storage-container description.</param>
    private static void ValidateRangeWithinContainer(nuint offset, int valueSize, uint containerSize, string containerDescription)
    {
        if (valueSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(valueSize), "The field value size must be positive.");

        ulong offsetValue = (ulong)offset;
        ulong end = checked(offsetValue + (uint)valueSize);

        if (end > containerSize)
            throw new InvalidDataException($"Field storage range [0x{offsetValue:X}, 0x{end:X}) lies outside {containerDescription} size 0x{containerSize:X}.");
    }

    /// <summary>Requires the complete remote value range to reside in committed readable memory.</summary>
    /// <param name="address">The first remote byte to validate.</param>
    /// <param name="size">The exact number of bytes required by the value.</param>
    private void ValidateReadable(nint address, int size)
    {
        if (!_memory.IsReadableRange(address, checked((nuint)size)))
            throw new InvalidDataException($"Field value range at 0x{address:X} with size 0x{size:X} is not completely readable.");
    }

    /// <summary>Adds a pointer-sized field offset to a remote base address while detecting arithmetic overflow.</summary>
    /// <param name="baseAddress">The remote storage base address.</param>
    /// <param name="offset">The byte offset to add.</param>
    /// <param name="description">The diagnostic description of the address calculation.</param>
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
