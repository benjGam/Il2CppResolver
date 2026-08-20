namespace UnityIl2CppResolver.Il2Cpp.Queries;

/// <summary>
/// Describes the semantic identity of a managed field requested from an IL2CPP target.
/// The query identifies the exact declaring type and field name without relying on runtime pointers, native offsets or backend-specific metadata structures.
/// </summary>
public sealed record FieldQuery
{
    /// <summary>
    /// Gets the semantic identity of the type declaring the requested field.
    /// </summary>
    public TypeQuery DeclaringType { get; }

    /// <summary>
    /// Gets the managed field name to resolve.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a semantic field query.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the declaring type.</param>
    /// <param name="namespaceName">The namespace containing the declaring type.</param>
    /// <param name="typeName">The managed declaring type name.</param>
    /// <param name="fieldName">The managed field name to resolve.</param>
    public FieldQuery(string assemblyName, string namespaceName, string typeName, string fieldName)
        : this(new TypeQuery(assemblyName, namespaceName, typeName), fieldName)
    {
    }

    /// <summary>
    /// Initializes a semantic field query from an existing declaring-type query.
    /// </summary>
    /// <param name="declaringType">The semantic declaring-type identity.</param>
    /// <param name="fieldName">The managed field name to resolve.</param>
    public FieldQuery(TypeQuery declaringType, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        DeclaringType = declaringType;
        Name = fieldName;
    }
}