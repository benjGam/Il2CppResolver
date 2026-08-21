using RuntimeMethodMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodMetadata;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Maintains session-scoped method metadata snapshots indexed by native <c>MethodInfo*</c> identity.
/// Metadata inspection remains lazy and independent from normal semantic method resolution and native code mapping.
/// </summary>
internal sealed class Il2CppMethodMetadataCatalog
{
    /// <summary>Provides live method metadata operations when an entry has not yet been cached.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Defines the timeout applied to individual remote runtime calls.</summary>
    private readonly TimeSpan _callTimeout;
    /// <summary>Stores method metadata indexed by native method identity.</summary>
    private readonly Dictionary<nint, RuntimeMethodMetadata> _metadata = new();

    /// <summary>Initializes a session-scoped method metadata catalogue.</summary>
    /// <param name="runtime">The live IL2CPP runtime used to inspect methods.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppMethodMetadataCatalog(Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _callTimeout = callTimeout;
    }

    /// <summary>Gets the cached or freshly inspected metadata associated with one runtime method.</summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> identity.</param>
    /// <returns>The immutable runtime method metadata snapshot.</returns>
    public RuntimeMethodMetadata GetMetadata(nint methodAddress)
    {
        if (_metadata.TryGetValue(methodAddress, out RuntimeMethodMetadata? metadata))
            return metadata;

        metadata = _runtime.GetMethodMetadata(methodAddress, _callTimeout);
        _metadata.Add(methodAddress, metadata);
        return metadata;
    }

    /// <summary>Clears every method metadata snapshot for the current resolver generation.</summary>
    public void Clear()
    {
        _metadata.Clear();
    }
}
