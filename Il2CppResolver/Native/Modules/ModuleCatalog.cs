using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Enumerates and exposes the native modules loaded by an attached target process.
/// This component belongs to the native discovery layer and transforms raw Windows module snapshot information into immutable <see cref="ProcessModuleInfo"/> instances.
/// Higher-level components use this catalog to locate candidate modules before performing PE, Unity or IL2CPP-specific inspection.
/// </summary>
public sealed class ModuleCatalog
{
    /// <summary>
    /// Represents the target process whose loaded modules are enumerated by this catalog.
    /// The target process remains the owner of the process lifetime, while module enumeration itself uses independent short-lived snapshot handles.
    /// </summary>
    private readonly TargetProcess _target;

    /// <summary>
    /// Initializes a module catalog for the specified target process.
    /// </summary>
    /// <param name="target">The validated target process whose loaded native modules will be enumerated.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public ModuleCatalog(TargetProcess target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    /// <summary>
    /// Enumerates the native modules currently loaded in the target process.
    /// A new Windows module snapshot is created for each call so the returned collection reflects the process state observed at enumeration time.
    /// </summary>
    /// <returns>An immutable snapshot containing descriptions of all modules reported by the operating system.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot create or enumerate the process module snapshot.
    /// </exception>
    public IReadOnlyList<ProcessModuleInfo> Enumerate()
    {
        _target.ThrowIfExited();

        ModuleSnapshotFlags snapshotFlags = ModuleSnapshotFlags.Module | ModuleSnapshotFlags.Module32;

        using SafeSnapshotHandle snapshot = NativeMethods.CreateToolhelp32Snapshot(snapshotFlags, (uint)_target.ProcessId);

        if (snapshot.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to create a module snapshot for process {_target.ProcessId}.");

        ModuleEntry32 entry = ModuleEntry32.Create();
        List<ProcessModuleInfo> modules = new();

        bool succeeded = NativeMethods.Module32First(snapshot, ref entry);

        if (!succeeded)
        {
            int errorCode = Marshal.GetLastWin32Error();

            if (errorCode == NativeMethods.ErrorNoMoreFiles)
                return modules.AsReadOnly();

            throw new Win32Exception(errorCode, $"Unable to enumerate modules for process {_target.ProcessId}.");
        }

        do
        {
            modules.Add(CreateModuleInfo(entry));
            entry = ModuleEntry32.Create();
        }
        while (NativeMethods.Module32Next(snapshot, ref entry));

        int lastError = Marshal.GetLastWin32Error();

        if (lastError != NativeMethods.ErrorNoMoreFiles)
            throw new Win32Exception(lastError, $"Module enumeration failed for process {_target.ProcessId}.");

        return modules.AsReadOnly();
    }

    /// <summary>
    /// Searches the currently loaded module snapshot for a module matching the specified file name.
    /// Module names are compared using ordinal case-insensitive semantics because Windows module file names are not treated as case-sensitive identifiers by this layer.
    /// </summary>
    /// <param name="moduleName">The file name of the module to locate, such as <c>GameAssembly.dll</c>.</param>
    /// <returns>The matching module description when found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="moduleName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the process modules cannot be enumerated.
    /// </exception>
    public ProcessModuleInfo? Find(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        IReadOnlyList<ProcessModuleInfo> modules = Enumerate();

        foreach (ProcessModuleInfo module in modules)
        {
            if (string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase))
                return module;
        }

        return null;
    }

    /// <summary>
    /// Retrieves a required loaded module by file name.
    /// This method is intended for higher-level discovery components that cannot continue when a specific native module is absent.
    /// </summary>
    /// <param name="moduleName">The file name of the required module.</param>
    /// <returns>The matching loaded module description.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="moduleName"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has terminated or when the requested module is not loaded.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the process modules cannot be enumerated.
    /// </exception>
    public ProcessModuleInfo GetRequired(string moduleName)
    {
        ProcessModuleInfo? module = Find(moduleName);

        if (module is null)
            throw new InvalidOperationException($"Module '{moduleName}' is not loaded in process {_target.ProcessId}.");

        return module;
    }

    /// <summary>
    /// Converts a native Windows module snapshot entry into the immutable representation exposed by this catalog.
    /// </summary>
    /// <param name="entry">The native module entry populated by the Windows Tool Help API.</param>
    /// <returns>An immutable description of the loaded module.</returns>
    private static ProcessModuleInfo CreateModuleInfo(ModuleEntry32 entry)
    {
        return new ProcessModuleInfo(entry.ModuleName, entry.ExecutablePath, entry.BaseAddress, entry.ModuleSize);
    }
}