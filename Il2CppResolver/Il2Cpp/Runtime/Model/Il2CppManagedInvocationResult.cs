namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes the rooted managed result of one completed <c>il2cpp_runtime_invoke</c> call.
/// The return object may be zero for a null managed reference, while a non-zero exception address indicates that managed execution raised an exception; every non-null return object is retained by the accompanying strong GC handle until the caller explicitly releases or transfers that root.
/// </summary>
internal sealed record Il2CppManagedInvocationResult
{
    /// <summary>Gets the returned managed <c>Il2CppObject*</c>, or zero.</summary>
    public nint ReturnObjectAddress { get; }

    /// <summary>Gets the managed <c>Il2CppException*</c>, or zero when invocation completed without a managed exception.</summary>
    public nint ExceptionAddress { get; }

    /// <summary>Gets the opaque pointer-sized strong IL2CPP GC handle retaining the returned managed object, or zero for a null result.</summary>
    public nuint ReturnObjectGcHandle { get; }

    /// <summary>Initializes one immutable managed invocation result.</summary>
    /// <param name="returnObjectAddress">The returned managed object pointer.</param>
    /// <param name="exceptionAddress">The managed exception pointer, or zero.</param>
    /// <param name="returnObjectGcHandle">The opaque pointer-sized strong GC handle retaining the returned object, or zero for a null result.</param>
    public Il2CppManagedInvocationResult(nint returnObjectAddress, nint exceptionAddress, nuint returnObjectGcHandle)
    {
        ReturnObjectAddress = returnObjectAddress;
        ExceptionAddress = exceptionAddress;
        ReturnObjectGcHandle = returnObjectGcHandle;
    }
}
