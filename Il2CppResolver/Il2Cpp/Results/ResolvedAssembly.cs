using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents an assembly that has been successfully mapped from a semantic query to concrete entities inside an IL2CPP target.
/// The model preserves both the original query and the runtime identity required by subsequent type resolution without exposing how the backend discovered those values.
/// </summary>
public sealed record ResolvedAssembly
{
    /// <summary>
    /// Gets the semantic query that produced this resolved assembly.
    /// </summary>
    public AssemblyQuery Query { get; }

    /// <summary>
    /// Gets the canonical image name reported by the IL2CPP runtime.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the native address of the resolved <c>Il2CppAssembly</c> instance.
    /// </summary>
    public nint AssemblyAddress { get; }

    /// <summary>
    /// Gets the native address of the <c>Il2CppImage</c> associated with the resolved assembly.
    /// </summary>
    public nint ImageAddress { get; }

    /// <summary>
    /// Initializes the immutable result of a successful semantic assembly resolution.
    /// </summary>
    /// <param name="query">The semantic assembly query that was resolved.</param>
    /// <param name="name">The canonical image name reported by the target runtime.</param>
    /// <param name="assemblyAddress">The native <c>Il2CppAssembly*</c> address.</param>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> address.</param>
    internal ResolvedAssembly(AssemblyQuery query, string name, nint assemblyAddress, nint imageAddress)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (assemblyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(assemblyAddress), "The resolved IL2CPP assembly address cannot be zero.");

        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The resolved IL2CPP image address cannot be zero.");

        Query = query;
        Name = name;
        AssemblyAddress = assemblyAddress;
        ImageAddress = imageAddress;
    }
}