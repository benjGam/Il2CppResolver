namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Represents the completed result of a native function returning a pointer-sized value while also writing a pointer-sized unsigned value through an output argument.
/// This model belongs to the remote execution layer and preserves both values captured from a single short-lived remote invocation.
/// </summary>
internal sealed record RemoteCallPointerSizeOutResult
{
    /// <summary>
    /// Gets the native value returned by the invoked function through the x64 <c>RAX</c> register.
    /// </summary>
    public nint ReturnValue { get; }

    /// <summary>
    /// Gets the pointer-sized unsigned value written by the invoked function through its output argument.
    /// </summary>
    public nuint OutValue { get; }

    /// <summary>
    /// Gets the operating system exit code associated with the completed remote trampoline thread.
    /// </summary>
    public uint ThreadExitCode { get; }

    /// <summary>
    /// Initializes the immutable result of a completed native call containing both a return value and an output value.
    /// </summary>
    /// <param name="returnValue">The native value captured from the function return register.</param>
    /// <param name="outValue">The pointer-sized unsigned value written through the output argument.</param>
    /// <param name="threadExitCode">The termination code reported by the remote trampoline thread.</param>
    internal RemoteCallPointerSizeOutResult(nint returnValue, nuint outValue, uint threadExitCode)
    {
        ReturnValue = returnValue;
        OutValue = outValue;
        ThreadExitCode = threadExitCode;
    }
}