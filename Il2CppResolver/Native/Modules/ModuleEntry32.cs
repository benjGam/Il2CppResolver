using System.Runtime.InteropServices;

namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Represents the native <c>MODULEENTRY32W</c> structure used by the Windows Tool Help API during module enumeration.
/// This type is an interop-only representation and is converted into <see cref="ProcessModuleInfo"/> before information is exposed to higher architectural layers.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ModuleEntry32
{
    /// <summary>
    /// Contains the size of this structure and must be initialized before each Tool Help enumeration call.
    /// </summary>
    internal uint Size;

    /// <summary>
    /// Contains an internal module identifier reserved by the operating system.
    /// </summary>
    internal uint ModuleId;

    /// <summary>
    /// Contains an internal process identifier reported by the snapshot entry.
    /// </summary>
    internal uint ProcessId;

    /// <summary>
    /// Contains the module global reference count maintained by the operating system.
    /// </summary>
    internal uint GlobalUsageCount;

    /// <summary>
    /// Contains the module process-local reference count maintained by the operating system.
    /// </summary>
    internal uint ProcessUsageCount;

    /// <summary>
    /// Contains the virtual base address at which the module is mapped in the target process.
    /// </summary>
    internal nint BaseAddress;

    /// <summary>
    /// Contains the mapped image size of the module in bytes.
    /// </summary>
    internal uint ModuleSize;

    /// <summary>
    /// Contains the module handle reported by the operating system.
    /// </summary>
    internal nint ModuleHandle;

    /// <summary>
    /// Contains the file name of the module without its directory path.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    internal string ModuleName;

    /// <summary>
    /// Contains the absolute filesystem path of the module executable image.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    internal string ExecutablePath;

    /// <summary>
    /// Creates a correctly initialized native module entry suitable for use with <c>Module32FirstW</c> and <c>Module32NextW</c>.
    /// Windows requires the structure size to be supplied by the caller before each enumeration request.
    /// </summary>
    /// <returns>An initialized native module entry structure.</returns>
    internal static ModuleEntry32 Create()
    {
        ModuleEntry32 entry = new();
        entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();

        return entry;
    }
}