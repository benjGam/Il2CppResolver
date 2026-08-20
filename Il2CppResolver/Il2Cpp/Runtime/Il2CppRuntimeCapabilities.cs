namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Describes optional public IL2CPP runtime capabilities discovered from the target export table.
/// Capabilities allow higher layers to prefer stable runtime APIs while retaining explicit structural fallbacks for targets that do not expose them.
/// </summary>
internal sealed class Il2CppRuntimeCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the target exposes both public APIs required to obtain normal static-field storage without interpreting <c>Il2CppClass</c> internals.
    /// </summary>
    public bool HasStaticFieldStorageApi { get; }

    /// <summary>
    /// Initializes the capability snapshot from the resolved runtime export table.
    /// </summary>
    /// <param name="exports">The validated runtime exports used to determine optional capabilities.</param>
    public Il2CppRuntimeCapabilities(Il2CppRuntimeExports exports)
    {
        ArgumentNullException.ThrowIfNull(exports);
        HasStaticFieldStorageApi = exports.ClassGetStaticFieldData is not null && exports.ClassGetDataSize is not null;
    }
}
