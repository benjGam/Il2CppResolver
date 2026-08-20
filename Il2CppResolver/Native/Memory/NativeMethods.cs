using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Memory;

/// <summary>
/// Provides the Win32 virtual-memory primitives required by the read-only process memory abstraction.
/// This class contains interop declarations only and intentionally exposes no resolver-specific behavior.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Copies bytes from the virtual address space of a remote process into a local unmanaged buffer.
    /// </summary>
    /// <param name="process">The native handle of the process whose memory should be read.</param>
    /// <param name="baseAddress">The remote virtual address at which the read operation begins.</param>
    /// <param name="buffer">The local unmanaged buffer that receives the copied bytes.</param>
    /// <param name="size">The exact number of bytes requested from the target process.</param>
    /// <param name="bytesRead">Receives the number of bytes actually copied into the destination buffer.</param>
    /// <returns><see langword="true"/> when the native read operation succeeds; otherwise <see langword="false"/>.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool ReadProcessMemory(SafeProcessHandle process, nint baseAddress, void* buffer, nuint size, out nuint bytesRead);
}