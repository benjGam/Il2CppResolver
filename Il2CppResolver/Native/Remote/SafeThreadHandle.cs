using Microsoft.Win32.SafeHandles;

namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Owns a native Windows thread handle created by the remote execution layer.
/// The handle controls only the local reference to the remote thread object; releasing it does not terminate the underlying remote thread.
/// </summary>
internal sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Initializes an empty thread handle instance used by native interop marshalling.
    /// </summary>
    private SafeThreadHandle() : base(true)
    {
    }

    /// <summary>
    /// Releases the local native handle associated with the remote thread.
    /// </summary>
    /// <returns><see langword="true"/> when the handle was successfully released; otherwise <see langword="false"/>.</returns>
    protected override bool ReleaseHandle()
    {
        return NativeMethods.CloseHandle(handle);
    }
}