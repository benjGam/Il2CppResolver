namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Represents one completed attached IL2CPP runtime invocation and preserves the returned managed object, managed exception output, and strong GC handle retaining a non-null result.
/// The exception pointer is captured only as diagnostic identity and is not independently rooted after the invocation thread detaches.
/// </summary>
internal sealed record RemoteCallIl2CppInvokeResult
{
    /// <summary>Gets the managed <c>Il2CppObject*</c> returned by <c>il2cpp_runtime_invoke</c>, or zero.</summary>
    public nint ReturnValue { get; }

    /// <summary>Gets the diagnostic <c>Il2CppException*</c> written by <c>il2cpp_runtime_invoke</c>, or zero when no managed exception was raised.</summary>
    public nint ExceptionAddress { get; }

    /// <summary>Gets the opaque pointer-sized strong IL2CPP GC handle retaining the returned managed object, or zero when the getter returned null. The handle must never be narrowed to a 32-bit identifier.</summary>
    public nuint ReturnValueGcHandle { get; }

    /// <summary>Initializes one immutable IL2CPP runtime invocation result.</summary>
    /// <param name="returnValue">The returned managed object pointer.</param>
    /// <param name="exceptionAddress">The managed exception pointer, or zero.</param>
    /// <param name="returnValueGcHandle">The opaque pointer-sized strong GC handle retaining the returned object, or zero for a null result.</param>
    internal RemoteCallIl2CppInvokeResult(nint returnValue, nint exceptionAddress, nuint returnValueGcHandle)
    {
        ReturnValue = returnValue;
        ExceptionAddress = exceptionAddress;
        ReturnValueGcHandle = returnValueGcHandle;
    }
}
