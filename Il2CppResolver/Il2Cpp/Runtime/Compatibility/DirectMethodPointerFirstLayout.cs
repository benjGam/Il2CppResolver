namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Represents an IL2CPP <c>MethodInfo</c> layout in which the direct native <c>methodPointer</c> is the first pointer-sized field.
/// This profile makes that structural assumption explicit and isolates it from the rest of the resolver.
/// It must only be used when the target layout is known or successfully validated against runtime evidence.
/// </summary>
internal sealed class DirectMethodPointerFirstLayout : IIl2CppMethodInfoLayout
{
    /// <summary>
    /// Gets the shared stateless instance of this compatibility profile.
    /// </summary>
    public static DirectMethodPointerFirstLayout Instance { get; } = new();

    /// <summary>
    /// Gets the diagnostic identifier associated with this compatibility profile.
    /// </summary>
    public string Name => "DirectMethodPointerFirst";

    /// <summary>
    /// Gets the byte offset of the direct native method pointer within the supported <c>MethodInfo</c> layout.
    /// The direct pointer occupies the first field and therefore begins at offset zero.
    /// </summary>
    public int DirectMethodPointerOffset => 0;

    /// <summary>
    /// Prevents additional instances because this compatibility profile contains no mutable state.
    /// </summary>
    private DirectMethodPointerFirstLayout()
    {
    }
}