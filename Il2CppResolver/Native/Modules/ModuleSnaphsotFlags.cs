namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Defines the Windows Tool Help snapshot categories used when enumerating modules loaded by the target process.
/// The resolver requests both native and 32-bit module views so the native layer does not silently omit modules when Windows exposes both categories.
/// </summary>
[Flags]
internal enum ModuleSnapshotFlags : uint
{
    /// <summary>
    /// Includes modules associated with the specified process in the snapshot.
    /// </summary>
    Module = 0x00000008,

    /// <summary>
    /// Includes 32-bit modules when the caller executes on a 64-bit Windows environment.
    /// </summary>
    Module32 = 0x00000010
}