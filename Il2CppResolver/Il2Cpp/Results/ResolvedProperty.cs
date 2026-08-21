using System.Reflection;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed property resolved to a live IL2CPP <c>PropertyInfo</c>.
/// The result exposes the semantic property signature and reuses the session identity map for optional getter and setter methods instead of introducing separate accessor abstractions.
/// </summary>
public sealed class ResolvedProperty
{
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
    internal ResolvedProperty(PropertyQuery query, ResolvedType declaringType, nint propertyInfoAddress, string typeName, IReadOnlyList<string> indexParameterTypeNames, PropertyAttributes attributes, ResolvedMethod? getter, ResolvedMethod? setter)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(indexParameterTypeNames);

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
    }
}
