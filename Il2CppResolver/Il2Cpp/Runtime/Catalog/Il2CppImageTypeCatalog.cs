using RuntimeClassInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassInfo;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Stores a lazy local snapshot of every type exposed by one runtime <c>Il2CppImage</c>.
/// Image enumeration occurs at most once per cache generation and is used only by explicit navigation operations such as <c>ResolvedAssembly.GetTypes()</c>; targeted type resolution continues to use <c>il2cpp_class_from_name</c> directly.
/// </summary>
internal sealed class Il2CppImageTypeCatalog
{
    /// <summary>Provides the live IL2CPP operations used to materialize this image snapshot.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Identifies the native <c>Il2CppImage*</c> represented by this catalogue.</summary>
    private readonly nint _imageAddress;
    /// <summary>Defines the timeout applied to each remote runtime call used during image enumeration.</summary>
    private readonly TimeSpan _callTimeout;
    /// <summary>Stores the immutable class snapshot after its first successful enumeration.</summary>
    private IReadOnlyList<RuntimeClassInfo>? _types;

    /// <summary>Initializes a lazy image-type catalogue.</summary>
    /// <param name="runtime">The live IL2CPP runtime used to enumerate the image.</param>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> represented by this catalogue.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppImageTypeCatalog(Il2CppRuntime runtime, nint imageAddress, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image address cannot be zero.");

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _imageAddress = imageAddress;
        _callTimeout = callTimeout;
    }

    /// <summary>Gets every runtime class exposed by the image, materializing the complete image snapshot only once.</summary>
    /// <returns>The immutable runtime class snapshot.</returns>
    public IReadOnlyList<RuntimeClassInfo> GetTypes()
    {
        if (_types is null)
            _types = _runtime.GetClasses(_imageAddress, _callTimeout);

        return _types;
    }
}
