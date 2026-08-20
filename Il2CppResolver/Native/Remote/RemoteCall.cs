using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UnityIl2CppResolver.Native.Memory;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Executes narrowly scoped native function calls inside an attached Windows x64 target process.
/// This component belongs to the native remote execution layer and deliberately supports only explicit call signatures required by higher-level resolver backends.
/// The initial implementation invokes a parameterless x64 function, captures its complete 64-bit <c>RAX</c> return value through a short-lived trampoline and releases all remote resources only after remote thread termination has been proven.
/// </summary>
internal sealed class RemoteCall
{
    /// <summary>
    /// Represents the exact size in bytes of the x64 trampoline generated for a parameterless function returning a native pointer.
    /// </summary>
    private const int PointerReturnTrampolineSize = 36;

    /// <summary>
    /// Represents the largest finite timeout accepted by <c>WaitForSingleObject</c> without colliding with the native infinite-wait sentinel.
    /// </summary>
    private const uint MaximumFiniteTimeout = 0xFFFFFFFE;

    /// <summary>
    /// Represents the target process in which native functions are executed.
    /// The primary process handle remains read-only; stronger execution rights are obtained through short-lived secondary handles.
    /// </summary>
    private readonly TargetProcess _target;

    /// <summary>
    /// Provides read-only access to remote result buffers after a remote thread has completed.
    /// </summary>
    private readonly ProcessMemory _memory;

    /// <summary>
    /// Initializes a remote-call executor for the specified target process.
    /// </summary>
    /// <param name="target">The validated Windows x64 process in which native calls will be executed.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public RemoteCall(TargetProcess target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
        _memory = new ProcessMemory(target);
    }

    /// <summary>
    /// Invokes a parameterless native x64 function inside the target process and captures the complete pointer-sized value returned through <c>RAX</c>.
    /// A short-lived executable trampoline performs the call, writes the 64-bit return value into a separate remote result allocation and terminates with an exit code of zero.
    /// </summary>
    /// <param name="functionAddress">The validated remote address of the native function to invoke.</param>
    /// <param name="timeout">The maximum amount of time allowed for the remote thread to terminate.</param>
    /// <returns>The native function return value together with the completed remote thread exit code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="functionAddress"/> is zero or when <paramref name="timeout"/> is outside the supported finite wait range.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the remote thread does not terminate within the requested timeout. Remote allocations are deliberately abandoned in this situation because the thread may still reference them.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when remote allocation, memory initialization, protection, thread creation, synchronization or thread-state inspection fails.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the generated trampoline terminates with a non-zero thread exit code.
    /// </exception>
    public RemoteCallResult InvokePointer(nint functionAddress, TimeSpan timeout)
    {
        if (functionAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(functionAddress), "The remote function address cannot be zero.");

        uint timeoutMilliseconds = ConvertTimeout(timeout);

        ProcessAccessRights accessRights = ProcessAccessRights.CreateThread |
                                           ProcessAccessRights.QueryInformation |
                                           ProcessAccessRights.VirtualMemoryOperation |
                                           ProcessAccessRights.VirtualMemoryWrite |
                                           ProcessAccessRights.VirtualMemoryRead;

        using SafeProcessHandle executionHandle = _target.OpenAdditionalHandle(accessRights);
        using RemoteAllocation resultAllocation = RemoteAllocation.Allocate(executionHandle, sizeof(long), MemoryProtection.ReadWrite);
        using RemoteAllocation codeAllocation = RemoteAllocation.Allocate(executionHandle, PointerReturnTrampolineSize, MemoryProtection.ReadWrite);

        byte[] trampoline = BuildPointerReturnTrampoline(functionAddress, resultAllocation.Address);

        codeAllocation.Write(trampoline);
        codeAllocation.Protect(MemoryProtection.ExecuteRead);

        bool instructionCacheFlushed = NativeMethods.FlushInstructionCache(executionHandle, codeAllocation.Address, codeAllocation.Size);

        if (!instructionCacheFlushed)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to flush the instruction cache for remote trampoline 0x{codeAllocation.Address:X}.");

        using SafeThreadHandle thread = NativeMethods.CreateRemoteThread(executionHandle, 0, 0, codeAllocation.Address, 0, 0, out uint _);

        if (thread.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to create a remote thread in process {_target.ProcessId}.");

        uint waitResult = NativeMethods.WaitForSingleObject(thread, timeoutMilliseconds);

        if (waitResult == NativeMethods.WaitTimeout)
        {
            codeAllocation.Abandon();
            resultAllocation.Abandon();

            throw new TimeoutException($"Remote function call at 0x{functionAddress:X} did not complete within {timeout.TotalMilliseconds:F0} ms. Remote allocations were intentionally retained because thread termination could not be proven.");
        }

        if (waitResult == NativeMethods.WaitFailed)
        {
            int errorCode = Marshal.GetLastWin32Error();

            codeAllocation.Abandon();
            resultAllocation.Abandon();

            throw new Win32Exception(errorCode, $"Unable to wait for the remote thread executing function 0x{functionAddress:X}. Remote allocations were intentionally retained because thread termination could not be proven.");
        }

        if (waitResult != NativeMethods.WaitObject0)
        {
            codeAllocation.Abandon();
            resultAllocation.Abandon();

            throw new InvalidOperationException($"Unexpected wait result 0x{waitResult:X8} while executing remote function 0x{functionAddress:X}.");
        }

        bool exitCodeRead = NativeMethods.GetExitCodeThread(thread, out uint exitCode);

        if (!exitCodeRead)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to retrieve the exit code of the remote thread executing function 0x{functionAddress:X}.");

        if (exitCode != 0)
            throw new InvalidOperationException($"Remote trampoline executing function 0x{functionAddress:X} terminated with exit code 0x{exitCode:X8}.");

        nint returnValue = _memory.ReadPointer(resultAllocation.Address);

        return new RemoteCallResult(returnValue, exitCode);
    }

    /// <summary>
    /// Builds the minimal Windows x64 trampoline used to invoke a parameterless native function and persist its complete <c>RAX</c> result into remote memory.
    /// The generated code reserves the required x64 shadow space, maintains stack alignment, invokes the target function, stores its return value and exits with a zero thread status.
    /// </summary>
    /// <param name="functionAddress">The remote native function address loaded into <c>RAX</c> before invocation.</param>
    /// <param name="resultAddress">The writable remote address that receives the target function return value.</param>
    /// <returns>The complete machine-code sequence representing the remote trampoline.</returns>
    private static byte[] BuildPointerReturnTrampoline(nint functionAddress, nint resultAddress)
    {
        byte[] code = new byte[PointerReturnTrampolineSize];
        int offset = 0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xEC;
        code[offset++] = 0x28;

        code[offset++] = 0x48;
        code[offset++] = 0xB8;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), functionAddress.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0xFF;
        code[offset++] = 0xD0;

        code[offset++] = 0x48;
        code[offset++] = 0xBA;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), resultAddress.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0x48;
        code[offset++] = 0x89;
        code[offset++] = 0x02;

        code[offset++] = 0x31;
        code[offset++] = 0xC0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xC4;
        code[offset++] = 0x28;

        code[offset++] = 0xC3;

        if (offset != PointerReturnTrampolineSize)
            throw new InvalidOperationException($"Generated x64 trampoline size is {offset} byte(s), but {PointerReturnTrampolineSize} byte(s) were expected.");

        return code;
    }

    /// <summary>
    /// Converts a managed timeout into the finite millisecond representation accepted by the native wait API.
    /// </summary>
    /// <param name="timeout">The managed timeout requested by the caller.</param>
    /// <returns>The corresponding finite native timeout in milliseconds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the timeout is zero, negative or exceeds the largest finite native timeout value.
    /// </exception>
    private static uint ConvertTimeout(TimeSpan timeout)
    {
        double totalMilliseconds = timeout.TotalMilliseconds;

        if (double.IsNaN(totalMilliseconds) || totalMilliseconds <= 0 || totalMilliseconds > MaximumFiniteTimeout)
            throw new ArgumentOutOfRangeException(nameof(timeout), "The remote call timeout must represent a positive finite duration supported by WaitForSingleObject.");

        return checked((uint)Math.Ceiling(totalMilliseconds));
    }
}