using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Owns a native Windows Tool Help snapshot handle and guarantees deterministic release through the standard safe-handle lifetime mechanism.
/// Snapshot ownership is deliberately isolated from <see cref="UnityIl2CppResolver.Native.Process.TargetProcess"/> because each module enumeration operation creates a short-lived independent native resource.
/// </summary>
internal sealed class SafeSnapshotHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Initializes a new safe snapshot handle instance for native marshalling.
    /// </summary>
    private SafeSnapshotHandle() : base(true)
    {
    }

    /// <summary>
    /// Releases the underlying Windows snapshot handle when ownership ends.
    /// </summary>
    /// <returns><see langword="true"/> when the native handle was closed successfully; otherwise <see langword="false"/>.</returns>
    protected override bool ReleaseHandle()
    {
        return NativeMethods.CloseHandle(handle);
    }
}