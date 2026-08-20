namespace UnityIl2CppResolver.Il2Cpp.Resolution.Model;

/// <summary>
/// Describes the runtime storage category associated with a resolved managed field.
/// The category determines how the offset reported by IL2CPP must be interpreted and prevents instance, static, thread-static and literal fields from being treated as equivalent memory locations.
/// </summary>
public enum FieldStorageKind
{
    /// <summary>
    /// The field is stored directly inside each managed object or value-type instance.
    /// </summary>
    Instance,

    /// <summary>
    /// The field is stored inside the declaring class's IL2CPP static-field data block.
    /// </summary>
    Static,

    /// <summary>
    /// The field uses thread-local static storage whose concrete address depends on the executing managed thread.
    /// </summary>
    ThreadStatic,

    /// <summary>
    /// The field represents a metadata literal and does not expose ordinary mutable runtime storage.
    /// </summary>
    Literal
}