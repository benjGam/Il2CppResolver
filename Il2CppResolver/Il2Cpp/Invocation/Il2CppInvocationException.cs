namespace UnityIl2CppResolver.Il2Cpp.Invocation;

/// <summary>
/// Represents a managed exception captured from an IL2CPP method invocation performed through <c>il2cpp_runtime_invoke</c>.
/// The exception object remains owned by the target runtime; this exception exposes only its diagnostic remote identity, does not retain it with a GC handle, and does not attempt to marshal the managed exception object.
/// </summary>
public sealed class Il2CppInvocationException : Exception
{
    /// <summary>Gets the diagnostic remote <c>Il2CppException*</c> address reported by the target runtime. The address is not rooted for later dereferencing.</summary>
    public nint ExceptionAddress { get; }

    /// <summary>Initializes an invocation exception for one captured remote managed exception.</summary>
    /// <param name="message">The diagnostic message describing the failed managed invocation.</param>
    /// <param name="exceptionAddress">The non-null remote <c>Il2CppException*</c> identity.</param>
    internal Il2CppInvocationException(string message, nint exceptionAddress) : base(message)
    {
        if (exceptionAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(exceptionAddress), "The remote IL2CPP exception address cannot be zero.");

        ExceptionAddress = exceptionAddress;
    }
}
