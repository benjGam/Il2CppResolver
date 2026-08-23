using System.Reflection;
using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed property resolved to a live IL2CPP <c>PropertyInfo</c>.
/// The result exposes the semantic property signature and reuses the session identity map for optional getter and setter methods instead of introducing separate accessor abstractions.
/// </summary>
public sealed class ResolvedProperty
{
    /// <summary>Provides session-bound getter invocation while preserving generation invalidation.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this property identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic property identity associated with this resolved runtime entity.</summary>
    public PropertyQuery Query { get; }

    /// <summary>Gets the resolved runtime type declaring this property.</summary>
    public ResolvedType DeclaringType { get; }

    /// <summary>Gets the native address of the resolved IL2CPP <c>PropertyInfo</c>.</summary>
    public nint PropertyInfoAddress { get; }

    /// <summary>Gets the semantic managed property type name derived from its accessors.</summary>
    public string TypeName { get; }

    /// <summary>Gets the ordered semantic index-parameter type names associated with this property.</summary>
    public IReadOnlyList<string> IndexParameterTypeNames { get; }

    /// <summary>Gets the metadata attributes reported by IL2CPP for this property.</summary>
    public PropertyAttributes Attributes { get; }

    /// <summary>Gets the resolved getter method, or <see langword="null"/> when the property is not readable.</summary>
    public ResolvedMethod? Getter { get; }

    /// <summary>Gets the resolved setter method, or <see langword="null"/> when the property is not writable.</summary>
    public ResolvedMethod? Setter { get; }

    /// <summary>Gets a value indicating whether the property exposes a getter method.</summary>
    public bool CanRead => Getter is not null;

    /// <summary>Gets a value indicating whether the property exposes a setter method.</summary>
    public bool CanWrite => Setter is not null;

    /// <summary>Initializes an immutable resolved property result.</summary>
    /// <param name="query">The semantic property identity associated with the result.</param>
    /// <param name="declaringType">The resolved runtime declaring type.</param>
    /// <param name="propertyInfoAddress">The native <c>PropertyInfo*</c> address.</param>
    /// <param name="typeName">The semantic managed property type name.</param>
    /// <param name="indexParameterTypeNames">The ordered index-parameter type names.</param>
    /// <param name="attributes">The property metadata attributes reported by IL2CPP.</param>
    /// <param name="getter">The identity-mapped getter method, or <see langword="null"/> when absent.</param>
    /// <param name="setter">The identity-mapped setter method, or <see langword="null"/> when absent.</param>
    /// <param name="navigator">The owning session navigator used for getter invocation.</param>
    /// <param name="generation">The cache generation that produced this property identity.</param>
    internal ResolvedProperty(PropertyQuery query, ResolvedType declaringType, nint propertyInfoAddress, string typeName, IReadOnlyList<string> indexParameterTypeNames, PropertyAttributes attributes, ResolvedMethod? getter, ResolvedMethod? setter, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(indexParameterTypeNames);
        ArgumentNullException.ThrowIfNull(navigator);

        if (propertyInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyInfoAddress), "The resolved IL2CPP PropertyInfo address cannot be zero.");

        Query = query;
        DeclaringType = declaringType;
        PropertyInfoAddress = propertyInfoAddress;
        TypeName = typeName;
        IndexParameterTypeNames = Array.AsReadOnly(indexParameterTypeNames.ToArray());
        Attributes = attributes;
        Getter = getter;
        Setter = setter;
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>Invokes this property getter as an instance getter and reads one supported scalar return value.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> instance.</param>
    /// <returns>The validated scalar getter result.</returns>
    public T Read<T>(nint instanceAddress) where T : unmanaged => _navigator.ReadProperty<T>(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and reads one supported scalar return value.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <returns>The validated scalar getter result.</returns>
    public T ReadStatic<T>() where T : unmanaged => _navigator.ReadStaticProperty<T>(this, _generation);

    /// <summary>Invokes this property getter as an instance getter and reads the exact managed enum return type.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The validated enum getter result.</returns>
    public TEnum ReadEnum<TEnum>(nint instanceAddress) where TEnum : unmanaged, Enum => _navigator.ReadPropertyEnum<TEnum>(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and reads the exact managed enum return type.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <returns>The validated enum getter result.</returns>
    public TEnum ReadStaticEnum<TEnum>() where TEnum : unmanaged, Enum => _navigator.ReadStaticPropertyEnum<TEnum>(this, _generation);

    /// <summary>Invokes this property getter as an instance getter and returns the managed object reference result.</summary>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The returned <c>Il2CppObject*</c>, or zero for a null reference.</returns>
    public nint ReadReference(nint instanceAddress) => _navigator.ReadPropertyReference(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and returns the managed object reference result.</summary>
    /// <returns>The returned <c>Il2CppObject*</c>, or zero for a null reference.</returns>
    public nint ReadStaticReference() => _navigator.ReadStaticPropertyReference(this, _generation);

    /// <summary>Invokes this property getter as an instance getter and decodes a <c>System.String</c> result.</summary>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadString(nint instanceAddress) => _navigator.ReadPropertyString(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and decodes a <c>System.String</c> result.</summary>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadStaticString() => _navigator.ReadStaticPropertyString(this, _generation);

    /// <summary>Invokes this property getter as an instance getter and materializes a single-dimensional zero-based array result.</summary>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadArray(nint instanceAddress) => _navigator.ReadPropertyArray(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and materializes a single-dimensional zero-based array result.</summary>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadStaticArray() => _navigator.ReadStaticPropertyArray(this, _generation);

    /// <summary>Invokes this property getter as an instance getter and reads one explicitly validated blittable value-type result.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The raw blittable getter result.</returns>
    public T ReadBlittable<T>(nint instanceAddress) where T : unmanaged => _navigator.ReadPropertyBlittable<T>(this, instanceAddress, _generation);

    /// <summary>Invokes this property getter as a static getter and reads one explicitly validated blittable value-type result.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <returns>The raw blittable getter result.</returns>
    public T ReadStaticBlittable<T>() where T : unmanaged => _navigator.ReadStaticPropertyBlittable<T>(this, _generation);
}
