using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a loaded assembly resolved to concrete IL2CPP runtime identities and bound to the resolver session that produced it.
/// The object exposes immutable snapshot data directly and delegates navigation operations such as type enumeration or targeted child resolution back to its owning session.
/// </summary>
public sealed class ResolvedAssembly
{
    /// <summary>Provides session-bound navigation without exposing internal resolver infrastructure through the public API.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this runtime assembly identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic assembly identity associated with this resolved runtime entity.</summary>
    public AssemblyQuery Query { get; }
    /// <summary>Gets the canonical image name reported by the IL2CPP runtime.</summary>
    public string Name { get; }
    /// <summary>Gets the native address of the resolved <c>Il2CppAssembly</c> instance.</summary>
    public nint AssemblyAddress { get; }
    /// <summary>Gets the native address of the <c>Il2CppImage</c> associated with the resolved assembly.</summary>
    public nint ImageAddress { get; }

    /// <summary>Initializes an immutable session-bound assembly result.</summary>
    /// <param name="query">The semantic assembly identity associated with the result.</param>
    /// <param name="name">The canonical image name reported by the runtime.</param>
    /// <param name="assemblyAddress">The native <c>Il2CppAssembly*</c> address.</param>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> address.</param>
    /// <param name="navigator">The internal session navigator servicing child operations.</param>
    /// <param name="generation">The cache generation that produced this runtime identity.</param>
    internal ResolvedAssembly(AssemblyQuery query, string name, nint assemblyAddress, nint imageAddress, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(navigator);

        if (assemblyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(assemblyAddress), "The resolved IL2CPP assembly address cannot be zero.");

        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The resolved IL2CPP image address cannot be zero.");

        Query = query;
        Name = name;
        AssemblyAddress = assemblyAddress;
        ImageAddress = imageAddress;
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>
    /// Enumerates every runtime type exposed by this assembly image.
    /// The first call materializes a session-scoped image snapshot; subsequent calls in the same cache generation reuse local data.
    /// </summary>
    /// <returns>The resolved runtime types exposed by this assembly image.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target does not expose the optional IL2CPP image-class enumeration APIs.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this entity belongs to a cache generation invalidated by <c>ClearCache()</c>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the owning resolver session has been disposed.</exception>
    public IReadOnlyList<ResolvedType> GetTypes()
    {
        return _navigator.GetTypes(this, _generation);
    }

    /// <summary>
    /// Resolves one type relative to this assembly without enumerating every class in the image.
    /// This targeted path continues to use <c>il2cpp_class_from_name</c> and therefore remains cheaper than <see cref="GetTypes"/> when the semantic identity is already known.
    /// </summary>
    /// <param name="namespaceName">The exact managed namespace. An empty namespace is valid.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <returns>The resolved runtime type.</returns>
    public ResolvedType ResolveType(string namespaceName, string typeName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        return _navigator.ResolveType(this, namespaceName, typeName, _generation);
    }
}
