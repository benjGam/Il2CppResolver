using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Owns a short-lived virtual-memory allocation created inside a remote target process.
/// This class belongs exclusively to the remote execution layer and provides controlled write, protection and release operations over a single allocation.
/// Allocations are normally released deterministically, but ownership can be deliberately abandoned when a remote thread may still reference the region and safe cleanup cannot be proven.
/// </summary>
internal sealed class RemoteAllocation : IDisposable
{
    /// <summary>
    /// Represents the process handle used to create, modify and release this remote memory region.
    /// The handle is owned by the surrounding remote call and must outlive this allocation.
    /// </summary>
    private readonly SafeProcessHandle _process;

    /// <summary>
    /// Indicates whether ownership of the remote region has deliberately been abandoned because another remote execution context may still reference it.
    /// </summary>
    private bool _abandoned;

    /// <summary>
    /// Indicates whether the remote memory region has already been released.
    /// </summary>
    private bool _released;

    /// <summary>
    /// Gets the base virtual address of the allocation inside the target process.
    /// </summary>
    public nint Address { get; }

    /// <summary>
    /// Gets the requested size of the remote allocation in bytes.
    /// </summary>
    public nuint Size { get; }

    /// <summary>
    /// Initializes ownership of an already allocated remote virtual-memory region.
    /// </summary>
    /// <param name="process">The process handle owning the remote address space.</param>
    /// <param name="address">The base address returned by <c>VirtualAllocEx</c>.</param>
    /// <param name="size">The requested allocation size in bytes.</param>
    private RemoteAllocation(SafeProcessHandle process, nint address, nuint size)
    {
        _process = process;
        Address = address;
        Size = size;
    }

    /// <summary>
    /// Allocates committed and reserved memory inside the specified target process.
    /// </summary>
    /// <param name="process">The process handle whose virtual address space receives the allocation.</param>
    /// <param name="size">The number of bytes required by the allocation.</param>
    /// <param name="protection">The initial memory protection assigned to the allocation.</param>
    /// <returns>An owned <see cref="RemoteAllocation"/> representing the newly created region.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="size"/> is zero.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot allocate the requested remote memory.
    /// </exception>
    public static RemoteAllocation Allocate(SafeProcessHandle process, nuint size, MemoryProtection protection)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (size == 0)
            throw new ArgumentOutOfRangeException(nameof(size), "The remote allocation size cannot be zero.");

        MemoryAllocationType allocationType = MemoryAllocationType.Commit | MemoryAllocationType.Reserve;
        nint address = NativeMethods.VirtualAllocEx(process, 0, size, allocationType, protection);

        if (address == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to allocate {size} byte(s) inside the target process.");

        return new RemoteAllocation(process, address, size);
    }

    /// <summary>
    /// Writes a complete byte sequence into the beginning of the remote allocation.
    /// Partial writes are rejected so generated code and remote call data can never be consumed in an incomplete state.
    /// </summary>
    /// <param name="data">The byte sequence to copy into the remote allocation.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="data"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the supplied data exceeds the allocation size.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot write the complete byte sequence.
    /// </exception>
    public unsafe void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            throw new ArgumentException("The remote write buffer cannot be empty.", nameof(data));

        if ((nuint)data.Length > Size)
            throw new ArgumentOutOfRangeException(nameof(data), "The remote write buffer exceeds the allocation size.");

        fixed (byte* buffer = data)
        {
            bool succeeded = NativeMethods.WriteProcessMemory(_process, Address, buffer, (nuint)data.Length, out nuint bytesWritten);

            if (!succeeded)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to write {data.Length} byte(s) to remote address 0x{Address:X}.");

            if (bytesWritten != (nuint)data.Length)
                throw new Win32Exception($"Incomplete remote memory write at address 0x{Address:X}. Expected {data.Length} byte(s), but only {bytesWritten} byte(s) were written.");
        }
    }

    /// <summary>
    /// Changes the protection assigned to the remote allocation.
    /// Generated trampolines use this operation to transition from writable memory to executable read-only memory before execution.
    /// </summary>
    /// <param name="protection">The new memory protection to apply.</param>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot update the allocation protection.
    /// </exception>
    public void Protect(MemoryProtection protection)
    {
        bool succeeded = NativeMethods.VirtualProtectEx(_process, Address, Size, protection, out MemoryProtection _);

        if (!succeeded)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to change the protection of remote allocation 0x{Address:X}.");
    }

    /// <summary>
    /// Abandons ownership of the remote memory region without releasing it.
    /// This operation is used only when a remote thread may still execute from or reference the allocation and therefore releasing the memory would create a use-after-free condition inside the target process.
    /// </summary>
    public void Abandon()
    {
        _abandoned = true;
    }

    /// <summary>
    /// Releases the remote virtual-memory region when ownership remains local and the allocation has not already been released.
    /// Abandoned allocations are intentionally left mapped until the target process terminates.
    /// </summary>
    /// <exception cref="Win32Exception">
    /// Thrown when a locally owned remote allocation cannot be released.
    /// </exception>
    public void Dispose()
    {
        if (_released || _abandoned)
            return;

        bool succeeded = NativeMethods.VirtualFreeEx(_process, Address, 0, MemoryFreeType.Release);

        if (!succeeded)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to release remote allocation 0x{Address:X}.");

        _released = true;
    }
}