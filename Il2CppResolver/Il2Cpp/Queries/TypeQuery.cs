namespace UnityIl2CppResolver.Il2Cpp.Queries;

/// <summary>
/// Describes the semantic identity of a managed type requested from an IL2CPP target.
/// The query identifies the containing assembly, namespace and type name without exposing any backend-specific runtime or metadata representation.
/// </summary>
public sealed record TypeQuery
{
    /// <summary>
    /// Gets the semantic query identifying the assembly that contains the requested type.
    /// </summary>
    public AssemblyQuery Assembly { get; }

    /// <summary>
    /// Gets the managed namespace containing the requested type.
    /// An empty string represents a type declared in the global namespace.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the managed name of the requested type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a semantic type query from explicit assembly, namespace and type identifiers.
    /// </summary>
    /// <param name="assemblyName">The managed simple assembly name or runtime image name containing the requested type.</param>
    /// <param name="namespaceName">The managed namespace containing the requested type. An empty namespace is valid.</param>
    /// <param name="typeName">The managed type name to resolve.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="namespaceName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="assemblyName"/> or <paramref name="typeName"/> is empty or contains only whitespace.
    /// </exception>
    public TypeQuery(string assemblyName, string namespaceName, string typeName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        Assembly = new AssemblyQuery(assemblyName);
        Namespace = namespaceName;
        Name = typeName;
    }
}