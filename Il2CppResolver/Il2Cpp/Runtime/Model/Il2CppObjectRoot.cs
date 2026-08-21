namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Represents one managed object kept alive by an opaque pointer-sized strong IL2CPP GC handle.
/// The object address is resolved from the handle after creation, while the accompanying handle protects the managed object from collection until ownership is released.
/// </summary>
internal sealed class Il2CppObjectRoot
{
    /// <summary>Gets the managed <c>Il2CppObject*</c> resolved from the strong GC handle.</summary>
    public nint ObjectAddress { get; }

    /// <summary>Gets the opaque pointer-sized strong IL2CPP GC handle retaining the managed object.</summary>
    public nuint GcHandle { get; }

    /// <summary>Initializes one validated managed object root.</summary>
    /// <param name="objectAddress">The non-null managed object address resolved from the handle.</param>
    /// <param name="gcHandle">The non-zero opaque pointer-sized strong GC handle.</param>
    public Il2CppObjectRoot(nint objectAddress, nuint gcHandle)
    {
        if (objectAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(objectAddress), "The rooted IL2CPP object address cannot be zero.");

        if (gcHandle == 0)
            throw new ArgumentOutOfRangeException(nameof(gcHandle), "The IL2CPP GC handle cannot be zero.");

        ObjectAddress = objectAddress;
        GcHandle = gcHandle;
    }
}
