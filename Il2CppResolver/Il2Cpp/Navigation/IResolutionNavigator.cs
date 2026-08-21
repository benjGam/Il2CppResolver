using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Navigation;

/// <summary>
/// Defines the internal navigation contract used by resolved public entities to continue semantic traversal through the session that created them.
/// Every operation carries the originating cache generation so stale runtime pointers are rejected after session invalidation.
/// </summary>
internal interface IResolutionNavigator
{
    /// <summary>Gets every loaded runtime type exposed by the specified resolved assembly.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>The resolved types exposed by the assembly image.</returns>
    IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly, long generation);

    /// <summary>Resolves one type relative to an already resolved assembly without enumerating the complete image.</summary>
    /// <param name="assembly">The resolved assembly containing the requested type.</param>
    /// <param name="namespaceName">The exact managed namespace. An empty namespace is valid.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>The resolved runtime type.</returns>
    ResolvedType ResolveType(ResolvedAssembly assembly, string namespaceName, string typeName, long generation);

    /// <summary>Gets every method declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared methods with complete semantic signatures.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, long generation);

    /// <summary>Gets every overload declared by the specified type with the exact requested method name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All overloads sharing the requested name.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name, long generation);

    /// <summary>Resolves one exact method overload relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the overload.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    ResolvedMethod ResolveMethod(ResolvedType type, string methodName, IReadOnlyList<string> parameterTypeNames, long generation);

    /// <summary>Gets every property declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared properties with semantic signatures and optional accessors.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, long generation);

    /// <summary>Gets every property declared by the specified type with the exact requested property name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed property name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All matching properties.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, string name, long generation);

    /// <summary>Resolves one exact property relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="propertyName">The exact managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying the property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved property matching the requested signature.</returns>
    ResolvedProperty ResolveProperty(ResolvedType type, string propertyName, IReadOnlyList<string> indexParameterTypeNames, long generation);

    /// <summary>Gets every field declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared fields with semantic type and storage metadata.</returns>
    IReadOnlyList<ResolvedField> GetFields(ResolvedType type, long generation);

    /// <summary>Resolves one exact field relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    ResolvedField ResolveField(ResolvedType type, string fieldName, long generation);

    /// <summary>Maps an already resolved method to native code using the session's active layout-selection policy.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, long generation);

    /// <summary>Maps an already resolved method to native code using one explicit MethodInfo layout override.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout, long generation);

    /// <summary>Maps an already resolved normal static field to concrete storage using the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, long generation);

    /// <summary>Maps an already resolved normal static field to concrete storage using one explicit Il2CppClass layout override.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout, long generation);
}
