namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Identifies the mechanism used to resolve the concrete storage of a normal static IL2CPP field.
/// The value allows consumers to distinguish public runtime API resolution from version-sensitive structural layout interpretation.
/// </summary>
public enum FieldStorageResolutionSource
{
    /// <summary>
    /// The storage base and size were obtained through optional public IL2CPP runtime APIs.
    /// </summary>
    RuntimeApi,

    /// <summary>
    /// The storage base and size were obtained by interpreting <c>Il2CppClass</c> through an explicit structural layout.
    /// </summary>
    ClassLayout
}
