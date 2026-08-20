using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Memory;

/// <summary>
/// Provides the Win32 process-memory primitives required by the generic read-only memory inspection layer.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Copies memory from the target process into a local caller-provided buffer.
    /// </summary>
    /// <param name="process">The target process handle.</param>
    /// <param name="baseAddress">The remote address from which bytes should be read.</param>
    /// <param name="buffer">The local destination buffer.</param>
    /// <param name="size">The number of bytes requested.</param>
    /// <param name="bytesRead">Receives the number of bytes actually copied.</param>
    /// <returns><see langword="true"/> when the read succeeds; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern unsafe bool ReadProcessMemory(SafeProcessHandle process, nint baseAddress, void* buffer, nuint size, out nuint bytesRead);

    /// <summary>
    /// Retrieves information about the virtual-memory region containing the specified remote address.
    /// </summary>
    /// <param name="process">The target process whose address space should be inspected.</param>
    /// <param name="address">An address contained by the memory region to query.</param>
    /// <param name="information">Receives the native memory-region description.</param>
    /// <param name="informationLength">The size of the destination structure in bytes.</param>
    /// <returns>The number of bytes written to <paramref name="information"/>, or zero when the query fails.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(SafeProcessHandle process, nint address, out MemoryBasicInformation information, nuint informationLength);
}