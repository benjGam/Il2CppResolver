namespace UnityIl2CppResolver.Native.Process;

/// <summary>
/// Defines the native Windows access rights that can be requested when opening a target process.
/// The resolver deliberately requests only the permissions required by its native read-only inspection layer.
/// </summary>
[Flags]
internal enum ProcessAccessRights : uint
{
    /// <summary>
    /// Allows the caller to create a thread in the target process.
    /// This permission is requested only by the short-lived remote execution layer.
    /// </summary>
    CreateThread = 0x0002,

    /// <summary>
    /// Allows the caller to perform virtual-memory operations such as allocating, protecting and releasing remote memory.
    /// </summary>
    VirtualMemoryOperation = 0x0008,

    /// <summary>
    /// Allows the caller to write into the virtual address space of the target process.
    /// This permission is requested only while preparing short-lived remote call data and trampolines.
    /// </summary>
    VirtualMemoryWrite = 0x0020,

    /// <summary>
    /// Allows the caller to retrieve process information required by native remote-thread creation APIs.
    /// </summary>
    QueryInformation = 0x0400,

    /// <summary>
    /// Allows the caller to read memory from the target process through native virtual-memory APIs.
    /// </summary>
    VirtualMemoryRead = 0x0010,

    /// <summary>
    /// Allows limited process information queries such as architecture inspection.
    /// </summary>
    QueryLimitedInformation = 0x1000,

    /// <summary>
    /// Allows synchronization operations to wait on the process handle.
    /// This permission is required by <c>WaitForSingleObject</c> and is used to determine whether the target process has terminated.
    /// </summary>
    Synchronize = 0x00100000
}