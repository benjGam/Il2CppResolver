using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Provides the Win32 memory-management, synchronization and thread-creation primitives required by the short-lived remote execution layer.
/// This class contains unmanaged declarations only and exposes no IL2CPP-specific behavior.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// Represents the result returned by <c>WaitForSingleObject</c> when the remote thread has terminated.
    /// </summary>
    internal const uint WaitObject0 = 0x00000000;

    /// <summary>
    /// Represents the result returned by <c>WaitForSingleObject</c> when the requested timeout expires before the remote thread terminates.
    /// </summary>
    internal const uint WaitTimeout = 0x00000102;

    /// <summary>
    /// Represents the result returned by <c>WaitForSingleObject</c> when the wait operation itself fails.
    /// </summary>
    internal const uint WaitFailed = 0xFFFFFFFF;

    /// <summary>
    /// Allocates virtual memory inside the target process.
    /// </summary>
    /// <param name="process">The process handle whose virtual address space receives the allocation.</param>
    /// <param name="address">The preferred allocation address, or zero to allow the operating system to select one.</param>
    /// <param name="size">The requested allocation size in bytes.</param>
    /// <param name="allocationType">The reservation and commitment flags applied to the allocation.</param>
    /// <param name="protection">The initial memory protection assigned to the allocation.</param>
    /// <returns>The base address of the allocated remote region, or zero when allocation fails.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint VirtualAllocEx(SafeProcessHandle process, nint address, nuint size, MemoryAllocationType allocationType, MemoryProtection protection);

    /// <summary>
    /// Releases a complete virtual-memory region previously allocated inside the target process.
    /// </summary>
    /// <param name="process">The process handle owning the remote virtual-memory region.</param>
    /// <param name="address">The allocation base address returned by <c>VirtualAllocEx</c>.</param>
    /// <param name="size">The region size. This value must be zero when using <see cref="MemoryFreeType.Release"/>.</param>
    /// <param name="freeType">The release operation to perform.</param>
    /// <returns><see langword="true"/> when the allocation was successfully released; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFreeEx(SafeProcessHandle process, nint address, nuint size, MemoryFreeType freeType);

    /// <summary>
    /// Writes bytes from the current process into a writable virtual-memory range belonging to the target process.
    /// </summary>
    /// <param name="process">The process handle owning the destination virtual-memory range.</param>
    /// <param name="baseAddress">The remote destination address.</param>
    /// <param name="buffer">The local source buffer.</param>
    /// <param name="size">The exact number of bytes to write.</param>
    /// <param name="bytesWritten">Receives the number of bytes actually transferred.</param>
    /// <returns><see langword="true"/> when the write succeeds; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern unsafe bool WriteProcessMemory(SafeProcessHandle process, nint baseAddress, void* buffer, nuint size, out nuint bytesWritten);

    /// <summary>
    /// Changes the memory protection applied to an existing remote virtual-memory region.
    /// </summary>
    /// <param name="process">The process handle owning the memory region.</param>
    /// <param name="address">The base address of the region whose protection should be modified.</param>
    /// <param name="size">The number of bytes covered by the protection request.</param>
    /// <param name="newProtection">The new memory protection to apply.</param>
    /// <param name="oldProtection">Receives the previous protection associated with the region.</param>
    /// <returns><see langword="true"/> when the protection was changed successfully; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualProtectEx(SafeProcessHandle process, nint address, nuint size, MemoryProtection newProtection, out MemoryProtection oldProtection);

    /// <summary>
    /// Flushes the processor instruction cache for a region containing newly generated executable code.
    /// </summary>
    /// <param name="process">The process handle owning the executable region.</param>
    /// <param name="address">The beginning of the generated code region.</param>
    /// <param name="size">The size of the generated code region.</param>
    /// <returns><see langword="true"/> when the instruction cache was flushed successfully; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlushInstructionCache(SafeProcessHandle process, nint address, nuint size);

    /// <summary>
    /// Creates a new thread whose execution begins inside the virtual address space of the target process.
    /// </summary>
    /// <param name="process">The target process in which the thread should execute.</param>
    /// <param name="threadAttributes">Optional native thread security attributes, or zero to use the default behavior.</param>
    /// <param name="stackSize">The initial stack size, or zero to use the executable default.</param>
    /// <param name="startAddress">The remote executable address at which the new thread begins.</param>
    /// <param name="parameter">The optional argument supplied to the remote thread entry point.</param>
    /// <param name="creationFlags">Native thread creation flags.</param>
    /// <param name="threadId">Receives the operating system identifier assigned to the remote thread.</param>
    /// <returns>A safe handle owning the newly created remote thread handle.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeThreadHandle CreateRemoteThread(SafeProcessHandle process, nint threadAttributes, nuint stackSize, nint startAddress, nint parameter, uint creationFlags, out uint threadId);

    /// <summary>
    /// Waits for the remote thread to terminate or for the specified timeout to expire.
    /// </summary>
    /// <param name="thread">The remote thread handle to monitor.</param>
    /// <param name="milliseconds">The maximum number of milliseconds to wait.</param>
    /// <returns>The native wait result describing whether the thread terminated, timed out or the wait operation failed.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(SafeThreadHandle thread, uint milliseconds);

    /// <summary>
    /// Retrieves the termination status associated with a completed remote thread.
    /// </summary>
    /// <param name="thread">The remote thread whose termination status should be queried.</param>
    /// <param name="exitCode">Receives the thread exit code.</param>
    /// <returns><see langword="true"/> when the exit code was retrieved successfully; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetExitCodeThread(SafeThreadHandle thread, out uint exitCode);

    /// <summary>
    /// Releases a native Windows handle.
    /// </summary>
    /// <param name="handle">The native handle to release.</param>
    /// <returns><see langword="true"/> when the handle was released successfully; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);
}