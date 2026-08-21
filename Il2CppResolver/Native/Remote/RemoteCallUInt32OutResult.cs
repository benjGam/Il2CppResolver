namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Represents the result of a remote native function returning a 32-bit unsigned value and writing a second 32-bit unsigned value through an output pointer.
/// </summary>
internal sealed class RemoteCallUInt32OutResult
{
    /// <summary>Gets the 32-bit function return value.</summary>
    public uint ReturnValue { get; }
    /// <summary>Gets the 32-bit output value written through the native output pointer.</summary>
    public uint OutValue { get; }
    /// <summary>Gets the completed trampoline thread exit code.</summary>
    public uint ThreadExitCode { get; }

    /// <summary>Initializes an immutable remote-call result.</summary>
    /// <param name="returnValue">The 32-bit native return value.</param>
    /// <param name="outValue">The 32-bit value written through the output pointer.</param>
    /// <param name="threadExitCode">The completed trampoline thread exit code.</param>
    public RemoteCallUInt32OutResult(uint returnValue, uint outValue, uint threadExitCode)
    {
        ReturnValue = returnValue;
        OutValue = outValue;
        ThreadExitCode = threadExitCode;
    }
}
