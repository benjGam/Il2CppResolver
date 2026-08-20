using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Process;

/// <summary>
/// Provides the minimal Win32 process primitives required by the target-process abstraction.
/// This class is strictly limited to native interop declarations and contains no process-management or resolver-specific business logic.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Represents the value returned by <c>WaitForSingleObject</c> when the wait operation fails.
    /// When this value is returned, the associated Windows error code can be retrieved through <c>Marshal.GetLastWin32Error</c>.
    /// </summary>
    internal const uint WaitFailed = 0xFFFFFFFF;
    /// <summary>
    /// Represents the result returned by <c>WaitForSingleObject</c> when the target object is signaled.
    /// For a process handle, this indicates that the target process has terminated.
    /// </summary>
    internal const uint WaitObject0 = 0x00000000;

    /// <summary>
    /// Represents the result returned by <c>WaitForSingleObject</c> when the target object is not signaled before the requested timeout expires.
    /// With a zero timeout, this indicates that the target process is still running.
    /// </summary>
    internal const uint WaitTimeout = 0x00000102;

    /// <summary>
    /// Opens an existing process and returns a managed safe handle owning the resulting native process handle.
    /// </summary>
    /// <param name="desiredAccess">The native access rights requested for the process handle.</param>
    /// <param name="inheritHandle">Indicates whether child processes are allowed to inherit the returned handle.</param>
    /// <param name="processId">The operating system identifier of the process to open.</param>
    /// <returns>A safe handle representing the opened process, or an invalid handle when the native operation fails.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial SafeProcessHandle OpenProcess(ProcessAccessRights desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    /// <summary>
    /// Tests whether the supplied process handle is currently signaled.
    /// A process handle becomes signaled when the represented process terminates.
    /// </summary>
    /// <param name="handle">The process handle whose state should be queried.</param>
    /// <param name="milliseconds">The maximum number of milliseconds to wait before returning.</param>
    /// <returns>A native wait result identifying whether the process has terminated, remains active or the operation has failed.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    /// <summary>
    /// Retrieves the effective and native machine architectures associated with a process.
    /// The resolver uses this information to reject targets that do not satisfy its native x64 architecture requirement.
    /// </summary>
    /// <param name="process">The native process handle to inspect.</param>
    /// <param name="processMachine">Receives the process architecture when the target runs under an emulation layer, or <see cref="ImageFileMachine.Unknown"/> for a native process.</param>
    /// <param name="nativeMachine">Receives the native architecture of the operating system environment.</param>
    /// <returns><see langword="true"/> when architecture information was retrieved successfully; otherwise <see langword="false"/>.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(SafeProcessHandle process, out ImageFileMachine processMachine, out ImageFileMachine nativeMachine);
}