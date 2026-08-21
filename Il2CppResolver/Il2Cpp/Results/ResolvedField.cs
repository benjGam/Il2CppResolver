using System.Reflection;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed field resolved to a live IL2CPP <c>FieldInfo</c> and bound to the resolver session that produced it.
/// Semantic identity and storage category remain immutable, while concrete normal static-field storage is resolved explicitly through the owning session.
/// </summary>
public sealed class ResolvedField
{
    /// <summary>Provides session-bound navigation to concrete static-field storage mapping.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this FieldInfo identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic field identity associated with this resolved runtime entity.</summary>
    public FieldQuery Query { get; }
    /// <summary>Gets the resolved runtime type that declares this field.</summary>
    public ResolvedType DeclaringType { get; }
    /// <summary>Gets the native address of the resolved IL2CPP <c>FieldInfo</c>.</summary>
    public nint FieldInfoAddress { get; }
    /// <summary>Gets the semantic managed type name associated with the field.</summary>
    public string TypeName { get; }
    /// <summary>Gets the managed metadata attributes reported by IL2CPP for the field.</summary>
    public FieldAttributes Attributes { get; }
    /// <summary>Gets the runtime storage category associated with the field.</summary>
    public FieldStorageKind StorageKind { get; }
    /// <summary>Gets the object-relative byte offset when this is an instance field; otherwise <see langword="null"/>.</summary>
    public nuint? InstanceOffset { get; }
    /// <summary>Gets the class-static-data-relative byte offset when this is a normal static field; otherwise <see langword="null"/>.</summary>
    public nuint? StaticStorageOffset { get; }

    /// <summary>Initializes an immutable session-bound field result.</summary>
    /// <param name="query">The semantic field identity associated with the result.</param>
    /// <param name="declaringType">The resolved runtime type declaring the field.</param>
    /// <param name="fieldInfoAddress">The native <c>FieldInfo*</c> address.</param>
    /// <param name="typeName">The semantic managed field type name.</param>
    /// <param name="attributes">The metadata attributes reported by IL2CPP.</param>
    /// <param name="storageKind">The runtime storage category associated with the field.</param>
    /// <param name="instanceOffset">The object-relative instance offset when applicable.</param>
    /// <param name="staticStorageOffset">The class-static-data-relative offset when applicable.</param>
    /// <param name="navigator">The internal session navigator servicing concrete storage mapping.</param>
    /// <param name="generation">The cache generation that produced this runtime identity.</param>
    internal ResolvedField(FieldQuery query, ResolvedType declaringType, nint fieldInfoAddress, string typeName, FieldAttributes attributes, FieldStorageKind storageKind, nuint? instanceOffset, nuint? staticStorageOffset, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(navigator);

        if (fieldInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldInfoAddress), "The resolved IL2CPP FieldInfo address cannot be zero.");

        if (storageKind == FieldStorageKind.Instance && instanceOffset is null)
            throw new ArgumentException("An instance field must expose an instance offset.", nameof(instanceOffset));

        if (storageKind != FieldStorageKind.Instance && instanceOffset is not null)
            throw new ArgumentException("Only instance fields can expose an instance offset.", nameof(instanceOffset));

        if (storageKind == FieldStorageKind.Static && staticStorageOffset is null)
            throw new ArgumentException("A normal static field must expose a static storage offset.", nameof(staticStorageOffset));

        if (storageKind != FieldStorageKind.Static && staticStorageOffset is not null)
            throw new ArgumentException("Only normal static fields can expose a static storage offset.", nameof(staticStorageOffset));

        Query = query;
        DeclaringType = declaringType;
        FieldInfoAddress = fieldInfoAddress;
        TypeName = typeName;
        Attributes = attributes;
        StorageKind = storageKind;
        InstanceOffset = instanceOffset;
        StaticStorageOffset = staticStorageOffset;
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>Resolves concrete normal static-field storage using the field-storage strategy selected by the owning session.</summary>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveStorage()
    {
        return _navigator.ResolveFieldStorage(this, _generation);
    }

    /// <summary>Resolves concrete normal static-field storage using one explicit Il2CppClass layout override without changing session configuration.</summary>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveStorage(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ResolveFieldStorage(this, layout, _generation);
    }
}
