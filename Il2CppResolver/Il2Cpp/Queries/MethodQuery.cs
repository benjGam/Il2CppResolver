namespace UnityIl2CppResolver.Il2Cpp.Queries;

/// <summary>
/// Describes the semantic identity of a managed method requested from an IL2CPP target.
/// The query identifies the declaring type, method name and ordered parameter type names so overloaded methods can be distinguished without relying on native addresses or metadata indices.
/// </summary>
public sealed record MethodQuery
{
    /// <summary>
    /// Gets the semantic identity of the type declaring the requested method.
    /// </summary>
    public TypeQuery DeclaringType { get; }

    /// <summary>
    /// Gets the managed method name to resolve.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the ordered semantic parameter type names required by the requested overload.
    /// </summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>
    /// Initializes a semantic method query.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the declaring type.</param>
    /// <param name="namespaceName">The namespace containing the declaring type.</param>
    /// <param name="typeName">The managed declaring type name.</param>
    /// <param name="methodName">The managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the requested overload.</param>
    public MethodQuery(string assemblyName, string namespaceName, string typeName, string methodName, params string[] parameterTypeNames)
        : this(new TypeQuery(assemblyName, namespaceName, typeName), methodName, parameterTypeNames)
    {
    }

    /// <summary>
    /// Initializes a semantic method query from an existing declaring-type query.
    /// </summary>
    /// <param name="declaringType">The semantic declaring-type identity.</param>
    /// <param name="methodName">The managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the requested overload.</param>
    public MethodQuery(TypeQuery declaringType, string methodName, params string[] parameterTypeNames)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameterTypeNames);

        string[] parameters = parameterTypeNames.ToArray();

        if (parameters.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Method parameter type names cannot contain empty values.", nameof(parameterTypeNames));

        DeclaringType = declaringType;
        Name = methodName;
        ParameterTypeNames = Array.AsReadOnly(parameters);
    }
}