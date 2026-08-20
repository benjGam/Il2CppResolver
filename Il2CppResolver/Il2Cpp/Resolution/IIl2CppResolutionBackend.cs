using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Defines the semantic entity-resolution contract implemented by IL2CPP discovery backends.
/// Implementations may obtain their evidence from the live IL2CPP runtime, metadata files or future native analysis strategies, but consumers receive the same assembly, type and method resolution models regardless of the underlying source.
/// </summary>
internal interface IIl2CppResolutionBackend
{
    /// <summary>
    /// Resolves a semantic assembly query to a concrete IL2CPP assembly identity.
    /// </summary>
    /// <param name="query">The semantic assembly identity to resolve.</param>
    /// <returns>The resolved assembly and its backend-specific runtime evidence.</returns>
    ResolvedAssembly ResolveAssembly(AssemblyQuery query);

    /// <summary>
    /// Resolves a semantic type query to a concrete IL2CPP type identity.
    /// </summary>
    /// <param name="query">The semantic type identity to resolve.</param>
    /// <returns>The resolved type and its declaring assembly.</returns>
    ResolvedType ResolveType(TypeQuery query);

    /// <summary>
    /// Resolves a semantic method query to a concrete IL2CPP method identity.
    /// </summary>
    /// <param name="query">The complete semantic method signature to resolve.</param>
    /// <returns>The resolved method and its verified runtime signature.</returns>
    ResolvedMethod ResolveMethod(MethodQuery query);
}