namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Describes optional public IL2CPP runtime capabilities discovered from the target export table.
/// Capabilities keep process attachment permissive while allowing higher layers to reject only the specific feature whose required public APIs are unavailable.
/// </summary>
internal sealed class Il2CppRuntimeCapabilities
{
    /// <summary>Gets a value indicating whether the target can enumerate every type exposed by an IL2CPP image.</summary>
    public bool CanEnumerateImageTypes { get; }
    /// <summary>Gets a value indicating whether the target can enumerate properties and inspect their accessors.</summary>
    public bool CanEnumerateProperties { get; }
    /// <summary>Gets a value indicating whether the target exposes public normal static-field storage APIs.</summary>
    public bool HasStaticFieldStorageApi { get; }
    /// <summary>Gets a value indicating whether field <c>Il2CppType*</c> values can be classified for scalar, managed-reference and enum-category validation; enum underlying-type reads may require one additional export.</summary>
    public bool CanInspectFieldValueTypes { get; }
    /// <summary>Gets a value indicating whether complete public type metadata can be materialized.</summary>
    public bool CanInspectTypeMetadata { get; }
    /// <summary>Gets a value indicating whether complete public method metadata can be materialized.</summary>
    public bool CanInspectMethodMetadata { get; }

    /// <summary>Initializes the capability snapshot from the resolved runtime export table.</summary>
    /// <param name="exports">The validated runtime exports used to determine optional capabilities.</param>
    public Il2CppRuntimeCapabilities(Il2CppRuntimeExports exports)
    {
        ArgumentNullException.ThrowIfNull(exports);

        CanEnumerateImageTypes = exports.ImageGetClassCount is not null && exports.ImageGetClass is not null && exports.ClassGetName is not null && exports.ClassGetNamespace is not null;
        CanEnumerateProperties = exports.ClassGetProperties is not null && exports.PropertyGetName is not null && exports.PropertyGetFlags is not null && exports.PropertyGetGetMethod is not null && exports.PropertyGetSetMethod is not null;
        HasStaticFieldStorageApi = exports.ClassGetStaticFieldData is not null && exports.ClassGetDataSize is not null;
        CanInspectFieldValueTypes = exports.TypeGetType is not null && exports.ClassFromType is not null && exports.ClassIsValueType is not null && exports.ClassIsEnum is not null;
        CanInspectTypeMetadata = exports.ClassGetFlags is not null && exports.ClassGetTypeToken is not null && exports.ClassIsValueType is not null && exports.ClassIsEnum is not null && exports.ClassIsBlittable is not null && exports.ClassIsGeneric is not null && exports.ClassIsInflated is not null;
        CanInspectMethodMetadata = exports.MethodGetFlags is not null && exports.MethodIsGeneric is not null && exports.MethodIsInflated is not null && exports.MethodGetToken is not null;
    }
}
