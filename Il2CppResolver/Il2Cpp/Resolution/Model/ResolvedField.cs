using System.Reflection;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Resolution.Model;

/// <summary>
/// Represents a managed field successfully resolved to a live IL2CPP <c>FieldInfo</c>.
/// The result separates semantic identity from storage interpretation: instance fields expose an object-relative offset, normal static fields expose an offset within the declaring class's static-data block, while thread-static and literal fields deliberately expose no universal storage address.
/// </summary>
public sealed record ResolvedField
{
    /// <summary>
    /// Gets the semantic field query that produced this resolution result.
    /// </summary>
    public FieldQuery Query { get; }

    /// <summary>
    /// Gets the resolved runtime type that declares this field.
    /// </summary>
    public ResolvedType DeclaringType { get; }

    /// <summary>
    /// Gets the native address of the resolved IL2CPP <c>FieldInfo</c>.
    /// </summary>
    public nint FieldInfoAddress { get; }

    /// <summary>
    /// Gets the semantic managed type name associated with the field.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the managed metadata attributes reported by IL2CPP for the field.
    /// </summary>
    public FieldAttributes Attributes { get; }

    /// <summary>
    /// Gets the runtime storage category associated with the field.
    /// </summary>
    public FieldStorageKind StorageKind { get; }

    /// <summary>
    /// Gets the byte offset of an instance field relative to the beginning of its containing IL2CPP object or value-type instance.
    /// This property is populated only when <see cref="StorageKind"/> is <see cref="FieldStorageKind.Instance"/>.
    /// </summary>
    public nuint? InstanceOffset { get; }

    /// <summary>
    /// Gets the byte offset of a normal static field relative to the declaring class's IL2CPP static-field data block.
    /// This value is not an absolute process address because the static-data block itself is runtime-owned and version-sensitive.
    /// </summary>
    public nuint? StaticStorageOffset { get; }

    /// <summary>
    /// Initializes the immutable result of a successful semantic field resolution.
    /// </summary>
    /// <param name="query">The semantic field query that was resolved.</param>
    /// <param name="declaringType">The resolved runtime type declaring the field.</param>
    /// <param name="fieldInfoAddress">The native <c>FieldInfo*</c> address.</param>
    /// <param name="typeName">The semantic managed field type name.</param>
    /// <param name="attributes">The metadata attributes reported by IL2CPP.</param>
    /// <param name="storageKind">The runtime storage category associated with the field.</param>
    /// <param name="instanceOffset">The object-relative instance offset when applicable.</param>
    /// <param name="staticStorageOffset">The class-static-data-relative offset when applicable.</param>
    internal ResolvedField(FieldQuery query, ResolvedType declaringType, nint fieldInfoAddress, string typeName, FieldAttributes attributes, FieldStorageKind storageKind, nuint? instanceOffset, nuint? staticStorageOffset)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

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
    }
}