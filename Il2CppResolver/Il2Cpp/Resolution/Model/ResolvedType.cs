using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Resolution.Model;

/// <summary>
/// Represents a managed type that has been successfully mapped from a semantic query to its concrete <c>Il2CppClass</c> runtime entity.
/// The result retains its containing resolved assembly so subsequent method and field resolution can operate from a complete semantic identity.
/// </summary>
public sealed record ResolvedType
{
    /// <summary>
    /// Gets the semantic type query that produced this resolved runtime entity.
    /// </summary>
    public TypeQuery Query { get; }

    /// <summary>
    /// Gets the resolved assembly containing this type.
    /// </summary>
    public ResolvedAssembly Assembly { get; }

    /// <summary>
    /// Gets the native address of the resolved <c>Il2CppClass</c> instance.
    /// </summary>
    public nint ClassAddress { get; }

    /// <summary>
    /// Initializes the immutable result of a successful semantic type resolution.
    /// </summary>
    /// <param name="query">The semantic type query that was resolved.</param>
    /// <param name="assembly">The resolved assembly containing the type.</param>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> address returned by the target runtime.</param>
    internal ResolvedType(TypeQuery query, ResolvedAssembly assembly, nint classAddress)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(assembly);

        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The resolved IL2CPP class address cannot be zero.");

        Query = query;
        Assembly = assembly;
        ClassAddress = classAddress;
    }
}