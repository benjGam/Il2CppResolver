using System.Runtime.InteropServices;

namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Provides the Windows Tool Help and handle-management primitives required by native module enumeration.
/// This interop class contains no module discovery logic and exists solely as the unmanaged boundary used by <see cref="ModuleCatalog"/>.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// Represents the Windows error code returned when a Tool Help enumeration has reached the end of the available entries.
    /// </summary>
    internal const int ErrorNoMoreFiles = 18;

    /// <summary>
    /// Creates a read-only Tool Help snapshot containing the requested categories for the specified process.
    /// </summary>
    /// <param name="flags">The categories of process information that should be included in the snapshot.</param>
    /// <param name="processId">The operating system identifier of the process whose modules should be captured.</param>
    /// <returns>A safe handle owning the created snapshot.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeSnapshotHandle CreateToolhelp32Snapshot(ModuleSnapshotFlags flags, uint processId);

    /// <summary>
    /// Retrieves the first module entry from an existing Tool Help snapshot.
    /// </summary>
    /// <param name="snapshot">The native snapshot handle containing module information.</param>
    /// <param name="entry">The initialized native structure that receives the first module entry.</param>
    /// <returns><see langword="true"/> when a module entry was returned; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", EntryPoint = "Module32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32First(SafeSnapshotHandle snapshot, ref ModuleEntry32 entry);

    /// <summary>
    /// Retrieves the next module entry from an existing Tool Help snapshot.
    /// </summary>
    /// <param name="snapshot">The native snapshot handle containing module information.</param>
    /// <param name="entry">The initialized native structure that receives the next module entry.</param>
    /// <returns><see langword="true"/> when another module entry was returned; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", EntryPoint = "Module32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32Next(SafeSnapshotHandle snapshot, ref ModuleEntry32 entry);

    /// <summary>
    /// Releases a native Windows handle owned by a safe-handle wrapper.
    /// </summary>
    /// <param name="handle">The native handle to release.</param>
    /// <returns><see langword="true"/> when the handle was released successfully; otherwise <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);
}