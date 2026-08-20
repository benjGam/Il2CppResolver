namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Describes the version-sensitive native layout information required to interpret an IL2CPP <c>MethodInfo</c> structure.
/// This interface forms the compatibility boundary between semantic method resolution and direct runtime-structure inspection.
/// Implementations must represent explicitly identified or validated layouts rather than embedding structural assumptions throughout the resolver.
/// </summary>
internal interface IIl2CppMethodInfoLayout
{
    /// <summary>
    /// Gets the diagnostic name identifying this compatibility layout.
    /// The name is preserved in native resolution results so callers can determine which structural assumption produced an address.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the byte offset of the direct native method pointer within an IL2CPP <c>MethodInfo</c> structure.
    /// </summary>
    int DirectMethodPointerOffset { get; }
}