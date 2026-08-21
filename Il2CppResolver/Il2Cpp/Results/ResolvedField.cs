using System.Reflection;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed field resolved to a live IL2CPP <c>FieldInfo</c> and bound to the resolver session that produced it.
/// Semantic identity and storage category remain immutable, while concrete storage mapping and explicitly supported value reading are delegated to the owning session so type, bounds and generation validation remain centralized.
/// </summary>
public sealed class ResolvedField
{
    /// <summary>Provides session-bound navigation to storage mapping and value reading.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this FieldInfo identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic field identity associated with this resolved runtime entity.</summary>
    public FieldQuery Query { get; }
    /// <summary>Gets the resolved runtime type that declares this field.</summary>
    public ResolvedType DeclaringType { get; }
    /// <summary>Gets the native address of the resolved IL2CPP <c>FieldInfo</c>.</summary>
    public nint FieldInfoAddress { get; }
    /// <summary>Gets the native <c>Il2CppType*</c> associated with this field for internal type validation.</summary>
    internal nint RuntimeTypeAddress { get; }
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
    /// <param name="declaringType">The resolved runtime declaring type.</param>
    /// <param name="fieldInfoAddress">The native <c>FieldInfo*</c> address.</param>
    /// <param name="runtimeTypeAddress">The native <c>Il2CppType*</c> associated with the field.</param>
    /// <param name="typeName">The semantic managed field type name.</param>
    /// <param name="attributes">The managed field attributes.</param>
    /// <param name="storageKind">The runtime storage category.</param>
    /// <param name="instanceOffset">The object-relative instance offset when applicable.</param>
    /// <param name="staticStorageOffset">The class-static-data-relative offset when applicable.</param>
    /// <param name="navigator">The internal session navigator servicing storage and value operations.</param>
    /// <param name="generation">The cache generation that produced this runtime identity.</param>
    internal ResolvedField(FieldQuery query, ResolvedType declaringType, nint fieldInfoAddress, nint runtimeTypeAddress, string typeName, FieldAttributes attributes, FieldStorageKind storageKind, nuint? instanceOffset, nuint? staticStorageOffset, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(navigator);

        if (fieldInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldInfoAddress), "The resolved IL2CPP FieldInfo address cannot be zero.");

        if (runtimeTypeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeTypeAddress), "The resolved IL2CPP field type address cannot be zero.");

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
        RuntimeTypeAddress = runtimeTypeAddress;
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

    /// <summary>Reads a supported unmanaged scalar from this normal static field using the session's active storage-selection policy.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the IL2CPP field.</typeparam>
    /// <returns>The validated scalar value read from the target process.</returns>
    public T ReadStatic<T>() where T : unmanaged
    {
        return _navigator.ReadStaticField<T>(this, _generation);
    }

    /// <summary>Reads a supported unmanaged scalar from this normal static field using one explicit class-layout override.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the IL2CPP field.</typeparam>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The validated scalar value read from the target process.</returns>
    public T ReadStatic<T>(Il2CppClassLayout layout) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticField<T>(this, layout, _generation);
    }

    /// <summary>Reads this normal static field as a managed object reference.</summary>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero when the field contains a null reference.</returns>
    public nint ReadStaticReference()
    {
        return _navigator.ReadStaticFieldReference(this, _generation);
    }

    /// <summary>Reads this normal static field as a managed object reference using one explicit class-layout override.</summary>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero when the field contains a null reference.</returns>
    public nint ReadStaticReference(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticFieldReference(this, layout, _generation);
    }

    /// <summary>Reads this normal static field as the exact requested managed enum type.</summary>
    /// <typeparam name="TEnum">The managed enum type matching the IL2CPP field enum.</typeparam>
    /// <returns>The enum value read from the target process.</returns>
    public TEnum ReadStaticEnum<TEnum>() where TEnum : unmanaged, Enum
    {
        return _navigator.ReadStaticFieldEnum<TEnum>(this, _generation);
    }

    /// <summary>Reads this normal static field as the exact requested managed enum type using one explicit class-layout override.</summary>
    /// <typeparam name="TEnum">The managed enum type matching the IL2CPP field enum.</typeparam>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The enum value read from the target process.</returns>
    public TEnum ReadStaticEnum<TEnum>(Il2CppClassLayout layout) where TEnum : unmanaged, Enum
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticFieldEnum<TEnum>(this, layout, _generation);
    }

    /// <summary>Reads this normal static field as a managed <c>System.String</c>.</summary>
    /// <returns>The decoded string, <see cref="string.Empty"/> for an empty string, or <see langword="null"/> for a null reference.</returns>
    public string? ReadStaticString()
    {
        return _navigator.ReadStaticFieldString(this, _generation);
    }

    /// <summary>Reads this normal static field as a managed <c>System.String</c> using one explicit class-layout override.</summary>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The decoded string, <see cref="string.Empty"/> for an empty string, or <see langword="null"/> for a null reference.</returns>
    public string? ReadStaticString(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticFieldString(this, layout, _generation);
    }

    /// <summary>Reads this normal static field as a single-dimensional zero-based managed array.</summary>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    public ResolvedArray? ReadStaticArray()
    {
        return _navigator.ReadStaticFieldArray(this, _generation);
    }

    /// <summary>Reads this normal static field as a single-dimensional zero-based managed array using one explicit class-layout override.</summary>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    public ResolvedArray? ReadStaticArray(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticFieldArray(this, layout, _generation);
    }

    /// <summary>Reads this normal static field as one explicitly validated blittable value type without managed marshalling.</summary>
    /// <typeparam name="T">The unmanaged managed value type whose semantic identity and native size must match the IL2CPP field.</typeparam>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadStaticBlittable<T>() where T : unmanaged
    {
        return _navigator.ReadStaticFieldBlittable<T>(this, _generation);
    }

    /// <summary>Reads this normal static field as one explicitly validated blittable value type using one explicit class-layout override.</summary>
    /// <typeparam name="T">The unmanaged managed value type whose semantic identity and native size must match the IL2CPP field.</typeparam>
    /// <param name="layout">The one-shot Il2CppClass structural layout used to map static storage.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadStaticBlittable<T>(Il2CppClassLayout layout) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ReadStaticFieldBlittable<T>(this, layout, _generation);
    }

    /// <summary>Reads a supported unmanaged scalar from this instance field relative to one remote IL2CPP object.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the IL2CPP field.</typeparam>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The validated scalar value read from the object instance.</returns>
    public T Read<T>(nint instanceAddress) where T : unmanaged
    {
        return _navigator.ReadInstanceField<T>(this, instanceAddress, _generation);
    }

    /// <summary>Reads this instance field as a managed object reference relative to one remote IL2CPP object.</summary>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The referenced remote <c>Il2CppObject*</c> address, or zero when the field contains a null reference.</returns>
    public nint ReadReference(nint instanceAddress)
    {
        return _navigator.ReadInstanceFieldReference(this, instanceAddress, _generation);
    }

    /// <summary>Reads this instance field as the exact requested managed enum type relative to one remote IL2CPP object.</summary>
    /// <typeparam name="TEnum">The managed enum type matching the IL2CPP field enum.</typeparam>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The enum value read from the object instance.</returns>
    public TEnum ReadEnum<TEnum>(nint instanceAddress) where TEnum : unmanaged, Enum
    {
        return _navigator.ReadInstanceFieldEnum<TEnum>(this, instanceAddress, _generation);
    }

    /// <summary>Reads this instance field as a managed <c>System.String</c> relative to one remote IL2CPP object.</summary>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The decoded string, <see cref="string.Empty"/> for an empty string, or <see langword="null"/> for a null reference.</returns>
    public string? ReadString(nint instanceAddress)
    {
        return _navigator.ReadInstanceFieldString(this, instanceAddress, _generation);
    }

    /// <summary>Reads this instance field as a single-dimensional zero-based managed array relative to one remote IL2CPP object.</summary>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    public ResolvedArray? ReadArray(nint instanceAddress)
    {
        return _navigator.ReadInstanceFieldArray(this, instanceAddress, _generation);
    }

    /// <summary>Reads this instance field as one explicitly validated blittable value type without managed marshalling.</summary>
    /// <typeparam name="T">The unmanaged managed value type whose semantic identity and native size must match the IL2CPP field.</typeparam>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadBlittable<T>(nint instanceAddress) where T : unmanaged
    {
        return _navigator.ReadInstanceFieldBlittable<T>(this, instanceAddress, _generation);
    }
}
