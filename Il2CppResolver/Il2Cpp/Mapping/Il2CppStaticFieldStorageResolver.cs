using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Mapping;

/// <summary>
/// Resolves the concrete process storage address of normal static IL2CPP fields by interpreting the declaring <c>Il2CppClass</c> through an explicit compatibility layout.
/// The resolver reads the class <c>static_fields</c> pointer and <c>static_fields_size</c>, validates the semantic field offset against that block and requires the complete referenced storage block to reside in committed readable process memory.
/// Thread-static, literal and instance fields are intentionally rejected because their storage models require different resolution strategies.
/// </summary>
internal sealed class Il2CppStaticFieldStorageResolver
{
    /// <summary>
    /// Represents the validated IL2CPP target whose runtime class structures and static storage are inspected.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Defines the explicit runtime <c>Il2CppClass</c> layout used to locate static storage metadata.
    /// </summary>
    private readonly Il2CppClassLayout _layout;

    /// <summary>
    /// Defines the maximum static-data block size accepted from an IL2CPP class layout.
    /// The limit is intentionally generous while preventing an incorrect structural offset from being interpreted as an arbitrary multi-gigabyte allocation size.
    /// </summary>
    private const uint MaximumStaticFieldsSize = 16 * 1024 * 1024;

    /// <summary>
    /// Initializes static-field storage resolution for the specified target and class-layout compatibility profile.
    /// </summary>
    /// <param name="target">The validated IL2CPP target containing the field runtime structures.</param>
    /// <param name="layout">The explicit <c>Il2CppClass</c> compatibility layout to apply.</param>
    public Il2CppStaticFieldStorageResolver(Il2CppTarget target, Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);

        _target = target;
        _layout = layout;
    }

    /// <summary>
    /// Resolves the concrete storage address associated with a normal static IL2CPP field.
    /// The operation validates the declaring class layout slots, static-data pointer, static-data size, field-relative offset, arithmetic result and final remote-memory readability before exposing an absolute process address.
    /// </summary>
    /// <param name="field">The semantically resolved field whose static storage should be located.</param>
    /// <returns>A validated static-field storage result containing both the calculated address and the evidence used to derive it.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="field"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="field"/> represents thread-static storage.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="field"/> is not a normal static field.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the selected class layout produces unreadable metadata, a null static-data block, an invalid static-data size, an out-of-range field offset or unreadable resulting storage.
    /// </exception>
    public ResolvedFieldStorage Resolve(ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.StorageKind == FieldStorageKind.ThreadStatic)
            throw new NotSupportedException($"Thread-static field '{field.Query.Name}' does not expose process-global static storage.");

        if (field.StorageKind != FieldStorageKind.Static)
            throw new InvalidOperationException($"Field '{field.Query.Name}' uses storage kind '{field.StorageKind}' and cannot be resolved through normal IL2CPP static storage.");

        if (field.StaticStorageOffset is not nuint staticStorageOffset)
            throw new InvalidDataException($"Static field '{field.Query.Name}' does not expose a static storage offset.");

        nint classAddress = field.DeclaringType.ClassAddress;
        nint staticFieldsPointerAddress = AddOffset(classAddress, _layout.StaticFieldsPointerOffset, "Il2CppClass.static_fields");
        nint staticFieldsSizeAddress = AddOffset(classAddress, _layout.StaticFieldsSizeOffset, "Il2CppClass.static_fields_size");

        if (!_target.Memory.IsReadableRange(staticFieldsPointerAddress, (nuint)IntPtr.Size))
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' places Il2CppClass.static_fields at unreadable address 0x{staticFieldsPointerAddress:X}.");

        if (!_target.Memory.IsReadableRange(staticFieldsSizeAddress, sizeof(uint)))
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' places Il2CppClass.static_fields_size at unreadable address 0x{staticFieldsSizeAddress:X}.");

        nint staticFieldsAddress = _target.Memory.ReadPointer(staticFieldsPointerAddress);
        uint staticFieldsSize = _target.Memory.Read<uint>(staticFieldsSizeAddress);

        if (staticFieldsAddress == 0)
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' produced a null static_fields pointer for class 0x{classAddress:X}.");

        if (staticFieldsSize == 0)
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' produced a zero static_fields_size for class 0x{classAddress:X}.");

        if (staticFieldsSize > MaximumStaticFieldsSize)
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' produced unreasonable static_fields_size 0x{staticFieldsSize:X} for class 0x{classAddress:X}.");

        if ((ulong)staticStorageOffset >= staticFieldsSize)
            throw new InvalidDataException($"Static field '{field.Query.Name}' has offset 0x{staticStorageOffset:X}, which lies outside the declaring class static-fields block of 0x{staticFieldsSize:X} byte(s).");

        if (!_target.Memory.IsReadableRange(staticFieldsAddress, staticFieldsSize))
            throw new InvalidDataException($"The static-fields block at 0x{staticFieldsAddress:X} with size 0x{staticFieldsSize:X} is not completely readable.");

        nint storageAddress = AddOffset(staticFieldsAddress, staticStorageOffset, "static field storage");

        if (!_target.Memory.IsReadableRange(storageAddress, 1))
            throw new InvalidDataException($"Calculated static field storage address 0x{storageAddress:X} is not readable.");

        return new ResolvedFieldStorage(
            field,
            staticFieldsAddress,
            staticFieldsSize,
            staticStorageOffset,
            storageAddress,
            FieldStorageResolutionSource.ClassLayout,
            _layout.Name,
            _layout.StaticFieldsPointerOffset,
            _layout.StaticFieldsSizeOffset);
    }

    /// <summary>
    /// Adds a signed structural offset to a native address while detecting arithmetic overflow.
    /// </summary>
    /// <param name="baseAddress">The native address from which the structural offset is applied.</param>
    /// <param name="offset">The non-negative byte offset to add.</param>
    /// <param name="description">The diagnostic description of the address being calculated.</param>
    /// <returns>The calculated native address.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the address calculation overflows the supported native address space.
    /// </exception>
    private static nint AddOffset(nint baseAddress, int offset, string description)
    {
        try
        {
            long address = checked(baseAddress.ToInt64() + offset);
            return checked((nint)address);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"Address calculation for {description} overflowed from base address 0x{baseAddress:X} with offset 0x{offset:X}.", exception);
        }
    }

    /// <summary>
    /// Adds a pointer-sized unsigned runtime offset to a native address while detecting arithmetic overflow.
    /// </summary>
    /// <param name="baseAddress">The native base address receiving the runtime offset.</param>
    /// <param name="offset">The pointer-sized unsigned byte offset to add.</param>
    /// <param name="description">The diagnostic description of the address being calculated.</param>
    /// <returns>The calculated native address.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the address calculation exceeds the supported native address space.
    /// </exception>
    private static nint AddOffset(nint baseAddress, nuint offset, string description)
    {
        ulong baseValue = unchecked((ulong)(nuint)baseAddress);
        ulong offsetValue = (ulong)offset;

        if (offsetValue > ulong.MaxValue - baseValue)
            throw new InvalidDataException($"Address calculation for {description} overflowed from base address 0x{baseAddress:X} with offset 0x{offset:X}.");

        ulong result = baseValue + offsetValue;

        if (result > long.MaxValue)
            throw new InvalidDataException($"Address calculation for {description} produced unsupported native address 0x{result:X}.");

        return (nint)(long)result;
    }
}