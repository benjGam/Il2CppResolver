using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed type resolved to a concrete IL2CPP <c>Il2CppClass</c> and bound to the resolver session that produced it.
/// The object exposes immutable semantic/runtime identity while delegating method and field navigation to session-scoped catalogues and caches.
/// </summary>
public sealed class ResolvedType
{
    /// <summary>Provides session-bound navigation without exposing internal resolver infrastructure through the public API.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this runtime class identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic type identity associated with this resolved runtime entity.</summary>
    public TypeQuery Query { get; }
    /// <summary>Gets the resolved assembly containing this type.</summary>
    public ResolvedAssembly Assembly { get; }
    /// <summary>Gets the native address of the resolved <c>Il2CppClass</c> instance.</summary>
    public nint ClassAddress { get; }

    /// <summary>Initializes an immutable session-bound type result.</summary>
    /// <param name="query">The semantic type identity associated with the result.</param>
    /// <param name="assembly">The resolved assembly containing the type.</param>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> address.</param>
    /// <param name="navigator">The internal session navigator servicing child operations.</param>
    /// <param name="generation">The cache generation that produced this runtime identity.</param>
    internal ResolvedType(TypeQuery query, ResolvedAssembly assembly, nint classAddress, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(navigator);

        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The resolved IL2CPP class address cannot be zero.");

        Query = query;
        Assembly = assembly;
        ClassAddress = classAddress;
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>
    /// Enumerates every method declared by this type and materializes complete semantic signatures only when this explicit navigation endpoint is requested.
    /// </summary>
    /// <returns>All methods declared by this runtime type.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods()
    {
        return _navigator.GetMethods(this, _generation);
    }

    /// <summary>
    /// Enumerates every overload declared by this type with the exact requested method name.
    /// Only matching candidates have their complete signatures inspected, preserving the targeted member-catalog optimization.
    /// </summary>
    /// <param name="name">The exact managed method name.</param>
    /// <returns>All declared overloads sharing the requested name.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _navigator.GetMethods(this, name, _generation);
    }

    /// <summary>Resolves one exact method overload relative to this already resolved declaring type.</summary>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the requested overload.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    public ResolvedMethod ResolveMethod(string methodName, params string[] parameterTypeNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameterTypeNames);

        if (parameterTypeNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Method parameter type names cannot contain empty values.", nameof(parameterTypeNames));

        return _navigator.ResolveMethod(this, methodName, Array.AsReadOnly(parameterTypeNames.ToArray()), _generation);
    }

    /// <summary>Enumerates every field declared by this runtime type.</summary>
    /// <returns>All declared fields together with semantic type and storage metadata.</returns>
    public IReadOnlyList<ResolvedField> GetFields()
    {
        return _navigator.GetFields(this, _generation);
    }

    /// <summary>Resolves one exact field relative to this already resolved declaring type.</summary>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    public ResolvedField ResolveField(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return _navigator.ResolveField(this, fieldName, _generation);
    }
}
