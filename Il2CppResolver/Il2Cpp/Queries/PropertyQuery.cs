namespace UnityIl2CppResolver.Il2Cpp.Queries;

/// <summary>
/// Describes the semantic identity of a managed property requested from an IL2CPP target.
/// The query identifies the declaring type, property name and ordered index-parameter type names so indexed properties can be distinguished without relying on runtime addresses.
/// </summary>
public sealed record PropertyQuery
{
    /// <summary>Gets the semantic identity of the type declaring the requested property.</summary>
    public TypeQuery DeclaringType { get; }

    /// <summary>Gets the managed property name to resolve.</summary>
    public string Name { get; }

    /// <summary>Gets the ordered semantic index-parameter type names identifying the requested property signature.</summary>
    public IReadOnlyList<string> IndexParameterTypeNames { get; }

    /// <summary>Initializes a semantic property query.</summary>
    /// <param name="assemblyName">The assembly containing the declaring type.</param>
    /// <param name="namespaceName">The namespace containing the declaring type.</param>
    /// <param name="typeName">The managed declaring type name.</param>
    /// <param name="propertyName">The managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying an indexed property.</param>
    public PropertyQuery(string assemblyName, string namespaceName, string typeName, string propertyName, params string[] indexParameterTypeNames)
        : this(new TypeQuery(assemblyName, namespaceName, typeName), propertyName, indexParameterTypeNames)
    {
    }

    /// <summary>Initializes a semantic property query from an existing declaring-type query.</summary>
    /// <param name="declaringType">The semantic declaring-type identity.</param>
    /// <param name="propertyName">The managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying an indexed property.</param>
    public PropertyQuery(TypeQuery declaringType, string propertyName, params string[] indexParameterTypeNames)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(indexParameterTypeNames);

        string[] parameters = indexParameterTypeNames.ToArray();

        if (parameters.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Property index parameter type names cannot contain empty values.", nameof(indexParameterTypeNames));

        DeclaringType = declaringType;
        Name = propertyName;
        IndexParameterTypeNames = Array.AsReadOnly(parameters);
    }
}
