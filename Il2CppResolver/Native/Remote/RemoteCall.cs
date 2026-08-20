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
    /// Represents the exact size in bytes of the x64 trampoline generated for a function receiving one pointer argument and one pointer-sized output argument.
    /// </summary>
    private const int PointerSizeOutTrampolineSize = 56;

    /// <summary>
    /// Represents the size in bytes of the remote result block containing both the native return value and the pointer-sized output value.
    /// </summary>
    private const int PointerSizeOutResultSize = 16;

    /// <summary>
    /// Represents the exact size in bytes of the x64 trampoline generated for a function receiving one pointer-sized argument and returning a pointer-sized value.
    /// </summary>
    private const int PointerArgumentTrampolineSize = 46;

    /// <summary>
    /// Represents the exact size in bytes of the x64 trampoline generated for a function receiving one pointer value and two remote buffer pointers.
    /// </summary>
    private const int PointerWithTwoBuffersTrampolineSize = 66;

    /// <summary>
    /// Represents the exact size in bytes of the x64 trampoline generated for a function receiving two immediate pointer-sized arguments and returning a pointer-sized value.
    /// </summary>
    private const int TwoArgumentPointerTrampolineSize = 56;

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
    /// Invokes a native x64 function receiving one pointer-sized argument and captures the complete pointer-sized value returned through <c>RAX</c>.
    /// The supplied argument is passed through the Windows x64 <c>RCX</c> register and the returned value is persisted into a short-lived remote result allocation.
    /// </summary>
    /// <param name="functionAddress">The validated remote address of the native function to invoke.</param>
    /// <param name="argument">The pointer-sized value passed to the native function through <c>RCX</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for the remote thread to terminate.</param>
    /// <returns>The native function return value together with the completed remote thread exit code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="functionAddress"/> is zero or when <paramref name="timeout"/> is outside the supported finite wait range.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the remote thread does not terminate within the requested timeout.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when a native remote execution operation fails.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the generated trampoline terminates abnormally.
    /// </exception>
    public RemoteCallResult InvokePointer(nint functionAddress, nint argument, TimeSpan timeout)
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
        using RemoteAllocation codeAllocation = RemoteAllocation.Allocate(executionHandle, PointerArgumentTrampolineSize, MemoryProtection.ReadWrite);

        byte[] trampoline = BuildPointerArgumentTrampoline(functionAddress, argument, resultAllocation.Address);

        codeAllocation.Write(trampoline);
        codeAllocation.Protect(MemoryProtection.ExecuteRead);

        uint exitCode = ExecuteTrampoline(executionHandle, codeAllocation, resultAllocation, functionAddress, timeoutMilliseconds);
        nint returnValue = _memory.ReadPointer(resultAllocation.Address);

        return new RemoteCallResult(returnValue, exitCode);
    }

    /// <summary>
    /// Invokes a native x64 function receiving two immediate pointer-sized arguments and returning a pointer-sized value.
    /// The arguments are passed through <c>RCX</c> and <c>RDX</c> according to the Windows x64 calling convention.
    /// </summary>
    /// <param name="functionAddress">The validated remote native function address to invoke.</param>
    /// <param name="firstArgument">The first argument passed through <c>RCX</c>.</param>
    /// <param name="secondArgument">The second argument passed through <c>RDX</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    /// <returns>The pointer-sized native return value and completed thread state.</returns>
    public RemoteCallResult InvokePointer(nint functionAddress, nint firstArgument, nuint secondArgument, TimeSpan timeout)
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
        using RemoteAllocation codeAllocation = RemoteAllocation.Allocate(executionHandle, TwoArgumentPointerTrampolineSize, MemoryProtection.ReadWrite);

        byte[] trampoline = BuildTwoArgumentPointerTrampoline(functionAddress, firstArgument, secondArgument, resultAllocation.Address);

        codeAllocation.Write(trampoline);
        codeAllocation.Protect(MemoryProtection.ExecuteRead);

        uint exitCode = ExecuteTrampoline(executionHandle, codeAllocation, resultAllocation, functionAddress, timeoutMilliseconds);
        nint returnValue = _memory.ReadPointer(resultAllocation.Address);

        return new RemoteCallResult(returnValue, exitCode);
    }

    /// <summary>
    /// Invokes a native function receiving one pointer argument and returning a 32-bit unsigned integer.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized function argument.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    /// <returns>The 32-bit unsigned value returned through <c>EAX</c>.</returns>
    public uint InvokeUInt32(nint functionAddress, nint argument, TimeSpan timeout)
    {
        RemoteCallResult result = InvokePointer(functionAddress, argument, timeout);
        return unchecked((uint)result.ReturnValue.ToInt64());
    }

    /// <summary>
    /// Invokes a native function receiving one pointer argument whose return value is intentionally ignored.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized function argument.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    public void InvokeVoid(nint functionAddress, nint argument, TimeSpan timeout)
    {
        _ = InvokePointer(functionAddress, argument, timeout);
    }

    /// <summary>
    /// Builds the Windows x64 trampoline used to invoke a function receiving two immediate arguments and returning a pointer-sized value.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="firstArgument">The first argument loaded into <c>RCX</c>.</param>
    /// <param name="secondArgument">The second argument loaded into <c>RDX</c>.</param>
    /// <param name="resultAddress">The writable remote address receiving <c>RAX</c>.</param>
    /// <returns>The complete generated x64 machine-code sequence.</returns>
    private static byte[] BuildTwoArgumentPointerTrampoline(nint functionAddress, nint firstArgument, nuint secondArgument, nint resultAddress)
    {
        byte[] code = new byte[TwoArgumentPointerTrampolineSize];
        int offset = 0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xEC;
        code[offset++] = 0x28;

        code[offset++] = 0x48;
        code[offset++] = 0xB9;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), firstArgument.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0x48;
        code[offset++] = 0xBA;
        BinaryPrimitives.WriteUInt64LittleEndian(code.AsSpan(offset, sizeof(ulong)), (ulong)secondArgument);
        offset += sizeof(ulong);

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

        if (offset != TwoArgumentPointerTrampolineSize)
            throw new InvalidOperationException($"Generated x64 trampoline size is {offset} byte(s), but {TwoArgumentPointerTrampolineSize} byte(s) were expected.");

        return code;
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
    /// Builds the Windows x64 trampoline used to invoke a native function receiving one pointer-sized argument and returning a pointer-sized value.
    /// The generated code places the argument in <c>RCX</c>, invokes the target function, stores the returned <c>RAX</c> value in remote memory and terminates cleanly.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized value passed to the function through <c>RCX</c>.</param>
    /// <param name="resultAddress">The writable remote address that receives the native function return value.</param>
    /// <returns>The complete generated x64 machine-code sequence.</returns>
    private static byte[] BuildPointerArgumentTrampoline(nint functionAddress, nint argument, nint resultAddress)
    {
        byte[] code = new byte[PointerArgumentTrampolineSize];
        int offset = 0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xEC;
        code[offset++] = 0x28;

        code[offset++] = 0x48;
        code[offset++] = 0xB9;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), argument.ToInt64());
        offset += sizeof(long);

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

        if (offset != PointerArgumentTrampolineSize)
            throw new InvalidOperationException($"Generated x64 trampoline size is {offset} byte(s), but {PointerArgumentTrampolineSize} byte(s) were expected.");

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

    /// <summary>
    /// Invokes a native x64 function receiving one pointer argument and one initially zero pointer-sized output argument.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized first argument.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    /// <returns>The native return value and the output value written by the function.</returns>
    public RemoteCallPointerSizeOutResult InvokePointerWithNuintOutArgument(nint functionAddress, nint argument, TimeSpan timeout)
    {
        return InvokePointerWithNuintRefArgument(functionAddress, argument, 0, timeout);
    }

    /// <summary>
    /// Invokes a native x64 function receiving one pointer argument followed by a pointer to mutable pointer-sized storage.
    /// The supplied initial value is written into remote storage before execution and the updated value is copied back after the remote thread has terminated.
    /// </summary>
    /// <param name="functionAddress">The validated remote native function address to invoke.</param>
    /// <param name="argument">The first pointer-sized argument passed through <c>RCX</c>.</param>
    /// <param name="initialValue">The initial pointer-sized value exposed to the native function through <c>RDX</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for the remote thread to terminate.</param>
    /// <returns>The native function return value together with the updated pointer-sized reference value.</returns>
    public RemoteCallPointerSizeOutResult InvokePointerWithNuintRefArgument(nint functionAddress, nint argument, nuint initialValue, TimeSpan timeout)
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
        using RemoteAllocation resultAllocation = RemoteAllocation.Allocate(executionHandle, PointerSizeOutResultSize, MemoryProtection.ReadWrite);
        using RemoteAllocation codeAllocation = RemoteAllocation.Allocate(executionHandle, PointerSizeOutTrampolineSize, MemoryProtection.ReadWrite);

        byte[] initialResult = new byte[PointerSizeOutResultSize];
        BinaryPrimitives.WriteUInt64LittleEndian(initialResult.AsSpan(sizeof(long), sizeof(ulong)), (ulong)initialValue);
        resultAllocation.Write(initialResult);

        nint outValueAddress = resultAllocation.Address + sizeof(long);
        byte[] trampoline = BuildPointerSizeOutTrampoline(functionAddress, argument, resultAllocation.Address, outValueAddress);

        codeAllocation.Write(trampoline);
        codeAllocation.Protect(MemoryProtection.ExecuteRead);

        uint exitCode = ExecuteTrampoline(executionHandle, codeAllocation, resultAllocation, functionAddress, timeoutMilliseconds);

        nint returnValue = _memory.ReadPointer(resultAllocation.Address);
        nuint outValue = _memory.Read<nuint>(outValueAddress);

        return new RemoteCallPointerSizeOutResult(returnValue, outValue, exitCode);
    }

    /// <summary>
    /// Invokes a native x64 function receiving one pointer-sized value followed by two pointers to remotely allocated buffers.
    /// The first argument is passed through <c>RCX</c>, while the remote buffer addresses are passed through <c>RDX</c> and <c>R8</c>.
    /// Both buffers remain owned by this operation and are abandoned together with the trampoline if remote thread termination cannot be proven.
    /// </summary>
    /// <param name="functionAddress">The validated remote address of the native function to invoke.</param>
    /// <param name="argument">The pointer-sized value passed through <c>RCX</c>.</param>
    /// <param name="secondArgumentBuffer">The data copied into remote memory and passed through <c>RDX</c>.</param>
    /// <param name="thirdArgumentBuffer">The data copied into remote memory and passed through <c>R8</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for the remote thread to terminate.</param>
    /// <returns>The native pointer-sized function return value together with the remote thread exit code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="functionAddress"/> is zero or when <paramref name="timeout"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when either remote argument buffer is empty.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when remote thread termination cannot be proven before the configured timeout expires.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when a native remote execution operation fails.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the generated trampoline terminates abnormally.
    /// </exception>
    public RemoteCallResult InvokePointerWithBufferArguments(nint functionAddress, nint argument, ReadOnlySpan<byte> secondArgumentBuffer, ReadOnlySpan<byte> thirdArgumentBuffer, TimeSpan timeout)
    {
        if (functionAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(functionAddress), "The remote function address cannot be zero.");

        if (secondArgumentBuffer.IsEmpty)
            throw new ArgumentException("The second remote argument buffer cannot be empty.", nameof(secondArgumentBuffer));

        if (thirdArgumentBuffer.IsEmpty)
            throw new ArgumentException("The third remote argument buffer cannot be empty.", nameof(thirdArgumentBuffer));

        uint timeoutMilliseconds = ConvertTimeout(timeout);

        ProcessAccessRights accessRights = ProcessAccessRights.CreateThread |
                                           ProcessAccessRights.QueryInformation |
                                           ProcessAccessRights.VirtualMemoryOperation |
                                           ProcessAccessRights.VirtualMemoryWrite |
                                           ProcessAccessRights.VirtualMemoryRead;

        using SafeProcessHandle executionHandle = _target.OpenAdditionalHandle(accessRights);
        using RemoteAllocation secondArgumentAllocation = RemoteAllocation.Allocate(executionHandle, (nuint)secondArgumentBuffer.Length, MemoryProtection.ReadWrite);
        using RemoteAllocation thirdArgumentAllocation = RemoteAllocation.Allocate(executionHandle, (nuint)thirdArgumentBuffer.Length, MemoryProtection.ReadWrite);
        using RemoteAllocation resultAllocation = RemoteAllocation.Allocate(executionHandle, sizeof(long), MemoryProtection.ReadWrite);
        using RemoteAllocation codeAllocation = RemoteAllocation.Allocate(executionHandle, PointerWithTwoBuffersTrampolineSize, MemoryProtection.ReadWrite);

        secondArgumentAllocation.Write(secondArgumentBuffer);
        thirdArgumentAllocation.Write(thirdArgumentBuffer);

        byte[] trampoline = BuildPointerWithTwoBuffersTrampoline(functionAddress, argument, secondArgumentAllocation.Address, thirdArgumentAllocation.Address, resultAllocation.Address);

        codeAllocation.Write(trampoline);
        codeAllocation.Protect(MemoryProtection.ExecuteRead);

        uint exitCode = ExecuteTrampoline(executionHandle, codeAllocation, resultAllocation, functionAddress, timeoutMilliseconds, secondArgumentAllocation, thirdArgumentAllocation);
        nint returnValue = _memory.ReadPointer(resultAllocation.Address);

        return new RemoteCallResult(returnValue, exitCode);
    }

    /// <summary>
    /// Builds the Windows x64 trampoline used to invoke a function receiving one pointer argument and one pointer-sized output argument.
    /// The generated code passes the first argument through <c>RCX</c>, passes remote output storage through <c>RDX</c>, captures <c>RAX</c> and exits cleanly.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized value passed through <c>RCX</c>.</param>
    /// <param name="resultAddress">The remote address that receives the native return value.</param>
    /// <param name="outValueAddress">The remote address passed through <c>RDX</c> for the pointer-sized output value.</param>
    /// <returns>The complete generated x64 machine-code sequence.</returns>
    private static byte[] BuildPointerSizeOutTrampoline(nint functionAddress, nint argument, nint resultAddress, nint outValueAddress)
    {
        byte[] code = new byte[PointerSizeOutTrampolineSize];
        int offset = 0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xEC;
        code[offset++] = 0x28;

        code[offset++] = 0x48;
        code[offset++] = 0xB9;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), argument.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0x48;
        code[offset++] = 0xBA;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), outValueAddress.ToInt64());
        offset += sizeof(long);

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

        if (offset != PointerSizeOutTrampolineSize)
            throw new InvalidOperationException($"Generated x64 trampoline size is {offset} byte(s), but {PointerSizeOutTrampolineSize} byte(s) were expected.");

        return code;
    }

    /// <summary>
    /// Builds the Windows x64 trampoline used to invoke a native function receiving one pointer value and two pointers to remote buffers.
    /// The generated code maps the arguments to <c>RCX</c>, <c>RDX</c> and <c>R8</c>, captures the returned <c>RAX</c> value and terminates cleanly.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized first argument passed through <c>RCX</c>.</param>
    /// <param name="secondArgumentAddress">The remote buffer address passed through <c>RDX</c>.</param>
    /// <param name="thirdArgumentAddress">The remote buffer address passed through <c>R8</c>.</param>
    /// <param name="resultAddress">The writable remote address that receives the function return value.</param>
    /// <returns>The complete generated x64 machine-code sequence.</returns>
    private static byte[] BuildPointerWithTwoBuffersTrampoline(nint functionAddress, nint argument, nint secondArgumentAddress, nint thirdArgumentAddress, nint resultAddress)
    {
        byte[] code = new byte[PointerWithTwoBuffersTrampolineSize];
        int offset = 0;

        code[offset++] = 0x48;
        code[offset++] = 0x83;
        code[offset++] = 0xEC;
        code[offset++] = 0x28;

        code[offset++] = 0x48;
        code[offset++] = 0xB9;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), argument.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0x48;
        code[offset++] = 0xBA;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), secondArgumentAddress.ToInt64());
        offset += sizeof(long);

        code[offset++] = 0x49;
        code[offset++] = 0xB8;
        BinaryPrimitives.WriteInt64LittleEndian(code.AsSpan(offset, sizeof(long)), thirdArgumentAddress.ToInt64());
        offset += sizeof(long);

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

        if (offset != PointerWithTwoBuffersTrampolineSize)
            throw new InvalidOperationException($"Generated x64 trampoline size is {offset} byte(s), but {PointerWithTwoBuffersTrampolineSize} byte(s) were expected.");

        return code;
    }

    /// <summary>
    /// Executes an already initialized remote trampoline and waits until its thread has provably terminated.
    /// When termination cannot be proven, every remote allocation potentially referenced by the thread is deliberately abandoned to prevent use-after-free conditions inside the target process.
    /// </summary>
    /// <param name="executionHandle">The process handle owning the remote executable allocations.</param>
    /// <param name="codeAllocation">The remote allocation containing the generated executable trampoline.</param>
    /// <param name="resultAllocation">The remote allocation containing the native function result.</param>
    /// <param name="functionAddress">The native function address used for diagnostic information.</param>
    /// <param name="timeoutMilliseconds">The finite native timeout applied to the remote thread wait operation.</param>
    /// <param name="referencedAllocations">Additional remote allocations that may remain referenced by the executing thread until termination.</param>
    /// <returns>The exit code reported by the completed remote trampoline thread.</returns>
    /// <exception cref="TimeoutException">
    /// Thrown when thread termination cannot be observed before the timeout expires.
    /// </exception>
    /// <exception cref="Win32Exception">
    /// Thrown when instruction cache synchronization, thread creation, synchronization or exit-code retrieval fails.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the wait operation returns an unexpected state or when the trampoline terminates with a non-zero exit code.
    /// </exception>
    private uint ExecuteTrampoline(SafeProcessHandle executionHandle, RemoteAllocation codeAllocation, RemoteAllocation resultAllocation, nint functionAddress, uint timeoutMilliseconds, params RemoteAllocation[] referencedAllocations)
    {
        bool instructionCacheFlushed = NativeMethods.FlushInstructionCache(executionHandle, codeAllocation.Address, codeAllocation.Size);

        if (!instructionCacheFlushed)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to flush the instruction cache for remote trampoline 0x{codeAllocation.Address:X}.");

        using SafeThreadHandle thread = NativeMethods.CreateRemoteThread(executionHandle, 0, 0, codeAllocation.Address, 0, 0, out uint _);

        if (thread.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to create a remote thread in process {_target.ProcessId}.");

        uint waitResult = NativeMethods.WaitForSingleObject(thread, timeoutMilliseconds);

        if (waitResult == NativeMethods.WaitTimeout)
        {
            AbandonReferencedAllocations(codeAllocation, resultAllocation, referencedAllocations);
            throw new TimeoutException($"Remote function call at 0x{functionAddress:X} did not complete before the configured timeout. Remote allocations were intentionally retained because thread termination could not be proven.");
        }

        if (waitResult == NativeMethods.WaitFailed)
        {
            int errorCode = Marshal.GetLastWin32Error();

            AbandonReferencedAllocations(codeAllocation, resultAllocation, referencedAllocations);
            throw new Win32Exception(errorCode, $"Unable to wait for the remote thread executing function 0x{functionAddress:X}. Remote allocations were intentionally retained because thread termination could not be proven.");
        }

        if (waitResult != NativeMethods.WaitObject0)
        {
            AbandonReferencedAllocations(codeAllocation, resultAllocation, referencedAllocations);
            throw new InvalidOperationException($"Unexpected wait result 0x{waitResult:X8} while executing remote function 0x{functionAddress:X}.");
        }

        bool exitCodeRead = NativeMethods.GetExitCodeThread(thread, out uint exitCode);

        if (!exitCodeRead)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to retrieve the exit code of the remote thread executing function 0x{functionAddress:X}.");

        if (exitCode != 0)
            throw new InvalidOperationException($"Remote trampoline executing function 0x{functionAddress:X} terminated with exit code 0x{exitCode:X8}.");

        return exitCode;
    }

    /// <summary>
    /// Abandons every remote allocation that may still be referenced by an executing remote thread.
    /// Abandoning intentionally leaks the affected regions inside the target process rather than risking execution or access through released memory.
    /// </summary>
    /// <param name="codeAllocation">The allocation containing the currently executing trampoline.</param>
    /// <param name="resultAllocation">The allocation containing native result storage.</param>
    /// <param name="referencedAllocations">Additional allocations potentially referenced by the remote call.</param>
    private static void AbandonReferencedAllocations(RemoteAllocation codeAllocation, RemoteAllocation resultAllocation, IReadOnlyList<RemoteAllocation> referencedAllocations)
    {
        codeAllocation.Abandon();
        resultAllocation.Abandon();

        foreach (RemoteAllocation allocation in referencedAllocations)
            allocation.Abandon();
    }
    /// <summary>
    /// Invokes a native function receiving one pointer argument and returns its pointer-sized unsigned result.
    /// This wrapper preserves every native result bit and is suitable for functions returning <c>size_t</c>.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized argument supplied through <c>RCX</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    /// <returns>The complete pointer-sized unsigned value returned by the native function.</returns>
    public nuint InvokeNuint(nint functionAddress, nint argument, TimeSpan timeout)
    {
        RemoteCallResult result = InvokePointer(functionAddress, argument, timeout);
        return unchecked((nuint)(ulong)result.ReturnValue.ToInt64());
    }

    /// <summary>
    /// Invokes a native function receiving one pointer argument and returns its signed 32-bit result.
    /// </summary>
    /// <param name="functionAddress">The remote native function address to invoke.</param>
    /// <param name="argument">The pointer-sized argument supplied through <c>RCX</c>.</param>
    /// <param name="timeout">The maximum amount of time allowed for execution.</param>
    /// <returns>The signed 32-bit value returned through <c>EAX</c>.</returns>
    public int InvokeInt32(nint functionAddress, nint argument, TimeSpan timeout)
    {
        RemoteCallResult result = InvokePointer(functionAddress, argument, timeout);
        return unchecked((int)(uint)result.ReturnValue.ToInt64());
    }

}