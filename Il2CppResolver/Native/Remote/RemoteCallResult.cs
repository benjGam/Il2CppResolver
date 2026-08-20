namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Represents the completed result of a short-lived native function invocation executed inside the target process.
/// The return value contains the full 64-bit value captured from the x64 <c>RAX</c> register, while the thread exit code describes the termination status of the generated trampoline itself.
/// </summary>
internal sealed record RemoteCallResult
{
    /// <summary>
    /// Gets the full native value returned by the invoked x64 function through the <c>RAX</c> register.
    /// </summary>
    public nint ReturnValue { get; }

    /// <summary>
    /// Gets the operating system exit code associated with the completed remote trampoline thread.
    /// The generated trampoline returns zero after successfully storing the native function result.
    /// </summary>
    public uint ThreadExitCode { get; }

    /// <summary>
    /// Initializes the immutable result of a completed remote function invocation.
    /// </summary>
    /// <param name="returnValue">The full native value captured from the target function return register.</param>
    /// <param name="threadExitCode">The termination code reported by the remote trampoline thread.</param>
    internal RemoteCallResult(nint returnValue, uint threadExitCode)
    {
        ReturnValue = returnValue;
        ThreadExitCode = threadExitCode;
    }
}