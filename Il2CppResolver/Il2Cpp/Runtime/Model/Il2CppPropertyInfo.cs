using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes a property discovered through the live IL2CPP runtime.
/// The immutable runtime model preserves the native <c>PropertyInfo*</c> identity, semantic property signature, metadata attributes and optional accessor <c>MethodInfo*</c> identities.
/// </summary>
internal sealed class Il2CppPropertyInfo
{
    /// <summary>Gets the native address of the runtime <c>PropertyInfo</c> structure.</summary>
    public nint PropertyAddress { get; }

    /// <summary>Gets the semantic managed property name.</summary>
    public string Name { get; }

    /// <summary>Gets the semantic managed property type name derived from its accessors.</summary>
    public string TypeName { get; }

    /// <summary>Gets the ordered semantic index-parameter type names derived from the property accessors.</summary>
    public IReadOnlyList<string> IndexParameterTypeNames { get; }

    /// <summary>Gets the metadata attributes reported by the IL2CPP runtime for this property.</summary>
    public PropertyAttributes Attributes { get; }

    /// <summary>Gets the native getter <c>MethodInfo*</c> address, or zero when the property is not readable.</summary>
    public nint GetterMethodAddress { get; }

    /// <summary>Gets the native setter <c>MethodInfo*</c> address, or zero when the property is not writable.</summary>
    public nint SetterMethodAddress { get; }

    /// <summary>Initializes an immutable runtime property description.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> address.</param>
    /// <param name="name">The semantic managed property name.</param>
    /// <param name="typeName">The semantic managed property type name.</param>
    /// <param name="indexParameterTypeNames">The ordered index-parameter type names.</param>
    /// <param name="attributes">The property metadata attributes reported by IL2CPP.</param>
    /// <param name="getterMethodAddress">The getter <c>MethodInfo*</c> address, or zero when absent.</param>
    /// <param name="setterMethodAddress">The setter <c>MethodInfo*</c> address, or zero when absent.</param>
    public Il2CppPropertyInfo(nint propertyAddress, string name, string typeName, IReadOnlyList<string> indexParameterTypeNames, PropertyAttributes attributes, nint getterMethodAddress, nint setterMethodAddress)
    {
        if (propertyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyAddress), "The IL2CPP property address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(indexParameterTypeNames);

        PropertyAddress = propertyAddress;
        Name = name;
        TypeName = typeName;
        IndexParameterTypeNames = Array.AsReadOnly(indexParameterTypeNames.ToArray());
        Attributes = attributes;
        GetterMethodAddress = getterMethodAddress;
        SetterMethodAddress = setterMethodAddress;
    }
}
