using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Defines the semantic resolution and explicit navigation boundary consumed by a resolver session independently from the concrete source of IL2CPP discovery data.
/// Targeted resolution operations remain separate from enumeration operations so known semantic identities never require complete runtime traversal.
/// </summary>
internal interface IIl2CppResolutionBackend
{
    /// <summary>Gets every loaded assembly from the session-scoped runtime snapshot.</summary>
    /// <returns>Every resolved assembly currently registered in the active runtime domain.</returns>
    IReadOnlyList<ResolvedAssembly> GetAssemblies();

    /// <summary>Resolves a semantic assembly query.</summary>
    /// <param name="query">The assembly query to resolve.</param>
    /// <returns>The resolved runtime assembly.</returns>
    ResolvedAssembly ResolveAssembly(AssemblyQuery query);

    /// <summary>Gets every type exposed by an already resolved assembly image.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <returns>Every resolved type exposed by the image.</returns>
    IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly);

    /// <summary>Resolves a semantic type query.</summary>
    /// <param name="query">The type query to resolve.</param>
    /// <returns>The resolved runtime type.</returns>
    ResolvedType ResolveType(TypeQuery query);

    /// <summary>Gets every method declared by an already resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <returns>Every declared method with complete semantic signatures.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type);

    /// <summary>Gets every overload declared by a type with the exact requested method name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <returns>Every matching overload.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name);

    /// <summary>Resolves a semantic method query.</summary>
    /// <param name="query">The method query to resolve.</param>
    /// <returns>The resolved runtime method.</returns>
    ResolvedMethod ResolveMethod(MethodQuery query);

    /// <summary>Gets every property declared by an already resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <returns>Every declared property.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type);

    /// <summary>Gets every property declared by a type with the exact requested property name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed property name.</param>
    /// <returns>Every matching property.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, string name);

    /// <summary>Resolves a semantic property query.</summary>
    /// <param name="query">The property query to resolve.</param>
    /// <returns>The resolved runtime property.</returns>
    ResolvedProperty ResolveProperty(PropertyQuery query);

    /// <summary>Gets every field declared by an already resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <returns>Every declared field.</returns>
    IReadOnlyList<ResolvedField> GetFields(ResolvedType type);

    /// <summary>Resolves a semantic field query.</summary>
    /// <param name="query">The field query to resolve.</param>
    /// <returns>The resolved runtime field.</returns>
    ResolvedField ResolveField(FieldQuery query);
}
