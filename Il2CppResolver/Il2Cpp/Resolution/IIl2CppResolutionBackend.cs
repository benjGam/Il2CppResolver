using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Defines the semantic resolution boundary consumed by a resolver session independently from the concrete source of IL2CPP discovery data.
/// </summary>
internal interface IIl2CppResolutionBackend
{
    /// <summary>Resolves a semantic assembly query.</summary>
    /// <param name="query">The assembly query to resolve.</param>
    /// <returns>The resolved runtime assembly.</returns>
    ResolvedAssembly ResolveAssembly(AssemblyQuery query);

    /// <summary>Resolves a semantic type query.</summary>
    /// <param name="query">The type query to resolve.</param>
    /// <returns>The resolved runtime type.</returns>
    ResolvedType ResolveType(TypeQuery query);

    /// <summary>Resolves a semantic method query.</summary>
    /// <param name="query">The method query to resolve.</param>
    /// <returns>The resolved runtime method.</returns>
    ResolvedMethod ResolveMethod(MethodQuery query);

    /// <summary>Resolves a semantic field query.</summary>
    /// <param name="query">The field query to resolve.</param>
    /// <returns>The resolved runtime field.</returns>
    ResolvedField ResolveField(FieldQuery query);
}
