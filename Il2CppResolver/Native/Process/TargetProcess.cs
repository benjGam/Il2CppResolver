using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace UnityIl2CppResolver.Native.Process;

/// <summary>
/// Represents an active attachment to a remote Windows x64 process.
/// This class owns the native process handle and establishes the basic process-level invariants required by the resolver.
/// It is the entry point of the native layer: memory access, module discovery and higher-level IL2CPP resolution components operate on top of a validated <see cref="TargetProcess"/> instance.
/// </summary>
public sealed class TargetProcess : IDisposable
{
    /// <summary>
    /// Owns the native handle associated with the target process.
    /// The handle remains valid for the lifetime of this instance and is released when <see cref="Dispose"/> is called.
    /// </summary>
    private readonly SafeProcessHandle _handle;

    /// <summary>
    /// Indicates whether this instance has already released its owned native resources.
    /// This prevents operations from being performed against an invalid process handle.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Gets the operating system identifier of the attached process.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets the native process handle used internally by the native layer.
    /// This property is intentionally not public because consumers of the resolver should never manipulate the process handle directly.
    /// </summary>
    internal SafeProcessHandle Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the target process has terminated.
    /// The check is performed directly against the owned native process handle and does not rely on a managed <see cref="System.Diagnostics.Process"/> instance.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the current <see cref="TargetProcess"/> instance has already been disposed.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot determine the current process state.
    /// </exception>
    public bool HasExited
    {
        get
        {
            ThrowIfDisposed();

            uint result = NativeMethods.WaitForSingleObject(_handle, 0);

            return result switch
            {
                NativeMethods.WaitObject0 => true,
                NativeMethods.WaitTimeout => false,
                NativeMethods.WaitFailed => throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to query the state of process {ProcessId}."),
                _ => throw new InvalidOperationException($"Unexpected wait result 0x{result:X8} while querying process {ProcessId}.")
            };
        }
    }

    /// <summary>
    /// Initializes a validated target process instance and transfers ownership of the supplied native process handle to it.
    /// This constructor is private because instances must be created through <see cref="Attach(int)"/> to guarantee that architecture and handle validation have been performed.
    /// </summary>
    /// <param name="processId">The operating system identifier of the target process.</param>
    /// <param name="handle">The validated native process handle whose ownership is transferred to this instance.</param>
    private TargetProcess(int processId, SafeProcessHandle handle)
    {
        ProcessId = processId;
        _handle = handle;
    }

    /// <summary>
    /// Attaches to an existing Windows x64 process and acquires the minimum native access rights required by the resolver native layer.
    /// The method validates the returned process handle and ensures that the target architecture matches the x64-only scope currently supported by the project.
    /// </summary>
    /// <param name="processId">The operating system identifier of the process to attach to.</param>
    /// <returns>A validated <see cref="TargetProcess"/> owning the acquired native process handle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="processId"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the current operating system is not Windows or when the target process is not a native x64 process.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the target process cannot be opened or when its architecture cannot be determined.
    /// </exception>
    public static TargetProcess Attach(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("TargetProcess currently supports Windows only.");

        ProcessAccessRights accessRights = ProcessAccessRights.QueryLimitedInformation |
                                   ProcessAccessRights.VirtualMemoryRead |
                                   ProcessAccessRights.Synchronize;

        SafeProcessHandle handle = NativeMethods.OpenProcess(accessRights, false, processId);

        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();

            throw new Win32Exception(errorCode, $"Unable to open process {processId}.");
        }

        try
        {
            EnsureSupportedArchitecture(handle, processId);
            return new TargetProcess(processId, handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Ensures that the target process is still running before an operation depending on its runtime state is executed.
    /// Higher-level native components can use this method when process termination must be distinguished from other memory or resolution failures.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the current <see cref="TargetProcess"/> instance has already been disposed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target process has already terminated.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot determine the current process state.
    /// </exception>
    public void ThrowIfExited()
    {
        ThrowIfDisposed();

        if (HasExited)
            throw new InvalidOperationException($"Target process {ProcessId} has exited.");
    }

    /// <summary>
    /// Releases the native process handle owned by this instance.
    /// Disposing a <see cref="TargetProcess"/> invalidates all native-layer components that depend on its handle.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _handle.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Validates that the target process is a native x64 process supported by the current resolver architecture.
    /// Establishing this invariant at attachment time allows every higher-level component to assume an eight-byte pointer size without repeating architecture checks.
    /// </summary>
    /// <param name="handle">The native handle associated with the target process.</param>
    /// <param name="processId">The operating system identifier used to provide contextual error messages.</param>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the target process is not a native x64 process.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when the operating system cannot determine the target process architecture.
    /// </exception>
    private static void EnsureSupportedArchitecture(SafeProcessHandle handle, int processId)
    {
        bool succeeded = NativeMethods.IsWow64Process2(handle, out ImageFileMachine processMachine, out ImageFileMachine nativeMachine);

        if (!succeeded)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to determine the architecture of process {processId}.");

        // IsWow64Process2 reports IMAGE_FILE_MACHINE_UNKNOWN when the target process uses the native architecture of the operating system.
        bool isNativeX64Process = processMachine == ImageFileMachine.Unknown && nativeMachine == ImageFileMachine.Amd64;

        if (!isNativeX64Process)
            throw new PlatformNotSupportedException($"Process {processId} is not a supported native x64 process.");
    }

    /// <summary>
    /// Ensures that the current instance still owns a valid process handle before an operation is performed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the current instance has already been disposed.
    /// </exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}