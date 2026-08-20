using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Native.Memory;

/// <summary>
/// Provides read-only access to the virtual memory of an attached target process.
/// This class belongs to the native foundation of the resolver and operates exclusively on top of a validated <see cref="TargetProcess"/>.
/// Higher-level components such as module inspection, PE parsing and IL2CPP discovery depend on this class instead of calling Win32 memory APIs directly.
/// </summary>
public sealed class ProcessMemory
{
    /// <summary>
    /// Represents the target process whose virtual memory is accessed by this instance.
    /// The process owns the native handle used by all read operations and therefore must remain alive and undisposed for the lifetime of this component.
    /// </summary>
    private readonly TargetProcess _target;

    /// <summary>
    /// Initializes a new read-only memory accessor for the specified target process.
    /// </summary>
    /// <param name="target">The validated target process whose virtual memory will be read.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public ProcessMemory(TargetProcess target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
    }

    /// <summary>
    /// Reads an unmanaged value from the specified virtual address in the target process.
    /// This method is intended for fixed-size native structures, primitive values and pointers required by the resolver layers.
    /// </summary>
    /// <typeparam name="T">The unmanaged value type to read from the target process.</typeparam>
    /// <param name="address">The remote virtual address from which the value should be read.</param>
    /// <returns>The unmanaged value reconstructed from the target process memory.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="address"/> is zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot read the requested memory range or when the read is incomplete.
    /// </exception>
    public unsafe T Read<T>(nint address) where T : unmanaged
    {
        ValidateAddress(address);

        T value = default;
        Span<byte> buffer = new(&value, sizeof(T));

        ReadBytes(address, buffer);

        return value;
    }

    /// <summary>
    /// Reads a fixed number of bytes from the specified virtual address in the target process.
    /// A new managed byte array containing exactly the requested number of bytes is allocated and returned to the caller.
    /// </summary>
    /// <param name="address">The remote virtual address from which the bytes should be read.</param>
    /// <param name="length">The exact number of bytes to read.</param>
    /// <returns>A byte array containing the requested memory range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="address"/> is zero or when <paramref name="length"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot read the requested memory range or when the read is incomplete.
    /// </exception>
    public byte[] ReadBytes(nint address, int length)
    {
        ValidateAddress(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        byte[] buffer = new byte[length];
        ReadBytes(address, buffer);

        return buffer;
    }

    /// <summary>
    /// Reads bytes from the specified virtual address directly into a caller-provided destination buffer.
    /// The complete destination span must be filled successfully; partial reads are treated as failures to prevent higher-level parsers from consuming incomplete native data.
    /// </summary>
    /// <param name="address">The remote virtual address from which the bytes should be read.</param>
    /// <param name="destination">The destination span that receives the remote memory contents.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="address"/> is zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot read the requested memory range or when the read is incomplete.
    /// </exception>
    public unsafe void ReadBytes(nint address, Span<byte> destination)
    {
        ValidateAddress(address);

        if (destination.IsEmpty)
            throw new ArgumentException("The destination buffer cannot be empty.", nameof(destination));

        _target.ThrowIfExited();

        fixed (byte* destinationPointer = destination)
        {
            bool succeeded = NativeMethods.ReadProcessMemory(_target.Handle, address, destinationPointer, (nuint)destination.Length, out nuint bytesRead);

            if (!succeeded)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to read {destination.Length} byte(s) from address 0x{address:X} in process {_target.ProcessId}.");

            if (bytesRead != (nuint)destination.Length)
                throw new Win32Exception($"Incomplete memory read at address 0x{address:X} in process {_target.ProcessId}. Expected {destination.Length} byte(s), but only {bytesRead} byte(s) were read.");
        }
    }

    /// <summary>
    /// Reads a native pointer from the specified virtual address in the target process.
    /// Because <see cref="TargetProcess"/> guarantees a native x64 target, the returned pointer is always read as an eight-byte value.
    /// </summary>
    /// <param name="address">The remote virtual address containing the native pointer.</param>
    /// <returns>The pointer value stored at the specified remote address.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="address"/> is zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the pointer cannot be read from the target process.
    /// </exception>
    public nint ReadPointer(nint address)
    {
        return Read<nint>(address);
    }

    /// <summary>
    /// Validates that a remote virtual address is suitable for a memory access request.
    /// This validation only rejects null addresses; address-range validation belongs to higher-level components that know the expected module or memory region.
    /// </summary>
    /// <param name="address">The remote virtual address to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="address"/> is zero.
    /// </exception>
    private static void ValidateAddress(nint address)
    {
        if (address == 0)
            throw new ArgumentOutOfRangeException(nameof(address), "The remote memory address cannot be zero.");
    }
}