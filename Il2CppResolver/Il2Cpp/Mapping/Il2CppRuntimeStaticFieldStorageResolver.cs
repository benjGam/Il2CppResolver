using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Native.Memory;

namespace UnityIl2CppResolver.Il2Cpp.Mapping;

/// <summary>
/// Resolves normal static-field storage through optional public IL2CPP class-storage APIs rather than interpreting version-sensitive <c>Il2CppClass</c> internals.
/// The resolver remains responsible for validating block size, field-relative offset, address arithmetic and target-memory readability before exposing a concrete storage address.
/// </summary>
internal sealed class Il2CppRuntimeStaticFieldStorageResolver
{
    /// <summary>
    /// Defines the defensive maximum static-data block size accepted from the runtime API.
    /// </summary>
    private const uint MaximumStaticFieldsSize = 16 * 1024 * 1024;

    /// <summary>
    /// Provides the optional IL2CPP runtime static-storage API.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Provides target-memory validation for runtime-returned storage ranges.
    /// </summary>
    private readonly ProcessMemory _memory;

    /// <summary>
    /// Defines the timeout applied to each runtime call.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Initializes runtime-API static-field storage resolution.
    /// </summary>
    /// <param name="runtime">The live IL2CPP runtime exposing optional class-storage APIs.</param>
    /// <param name="memory">The target-memory accessor used to validate returned storage.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppRuntimeStaticFieldStorageResolver(Il2CppRuntime runtime, ProcessMemory memory, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(memory);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _memory = memory;
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Resolves the concrete storage of a normal static field when the target exposes the required public IL2CPP runtime APIs.
    /// </summary>
    /// <param name="field">The semantically resolved normal static field.</param>
    /// <returns>The validated concrete storage mapping.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target does not expose the public static-field storage capability or when the field is thread-static.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the field is not a normal static field.</exception>
    /// <exception cref="InvalidDataException">Thrown when the runtime returns inconsistent storage metadata.</exception>
    public ResolvedFieldStorage Resolve(ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.StorageKind == FieldStorageKind.ThreadStatic)
            throw new NotSupportedException($"Thread-static field '{field.Query.Name}' does not expose process-global static storage.");

        if (field.StorageKind != FieldStorageKind.Static)
            throw new InvalidOperationException($"Field '{field.Query.Name}' uses storage kind '{field.StorageKind}' and cannot be resolved through normal IL2CPP static storage.");

        if (field.StaticStorageOffset is not nuint staticStorageOffset)
            throw new InvalidDataException($"Static field '{field.Query.Name}' does not expose a static storage offset.");

        if (!_runtime.TryGetStaticFieldStorage(field.DeclaringType.ClassAddress, _callTimeout, out nint staticFieldsAddress, out uint staticFieldsSize))
            throw new NotSupportedException("The target IL2CPP runtime does not expose the public static-field storage APIs required by this resolver path.");

        if (staticFieldsSize > MaximumStaticFieldsSize)
            throw new InvalidDataException($"IL2CPP returned unreasonable static-field data size 0x{staticFieldsSize:X} for class 0x{field.DeclaringType.ClassAddress:X}.");

        if ((ulong)staticStorageOffset >= staticFieldsSize)
            throw new InvalidDataException($"Static field '{field.Query.Name}' has offset 0x{staticStorageOffset:X}, which lies outside runtime-reported static-data size 0x{staticFieldsSize:X}.");

        if (!_memory.IsReadableRange(staticFieldsAddress, staticFieldsSize))
            throw new InvalidDataException($"The runtime-reported static-fields block at 0x{staticFieldsAddress:X} with size 0x{staticFieldsSize:X} is not completely readable.");

        nint storageAddress = AddOffset(staticFieldsAddress, staticStorageOffset);

        if (!_memory.IsReadableRange(storageAddress, 1))
            throw new InvalidDataException($"Calculated static field storage address 0x{storageAddress:X} is not readable.");

        return new ResolvedFieldStorage(field, staticFieldsAddress, staticFieldsSize, staticStorageOffset, storageAddress, FieldStorageResolutionSource.RuntimeApi, null, null, null);
    }

    /// <summary>
    /// Adds a pointer-sized unsigned runtime offset to a native base address while detecting overflow.
    /// </summary>
    /// <param name="baseAddress">The native base address receiving the offset.</param>
    /// <param name="offset">The byte offset to add.</param>
    /// <returns>The resulting native address.</returns>
    private static nint AddOffset(nint baseAddress, nuint offset)
    {
        ulong baseValue = unchecked((ulong)(nuint)baseAddress);
        ulong offsetValue = (ulong)offset;

        if (offsetValue > ulong.MaxValue - baseValue)
            throw new InvalidDataException($"Static field storage address calculation overflowed from base 0x{baseAddress:X} with offset 0x{offset:X}.");

        ulong result = baseValue + offsetValue;

        if (result > long.MaxValue)
            throw new InvalidDataException($"Static field storage address calculation produced unsupported native address 0x{result:X}.");

        return (nint)(long)result;
    }
}
