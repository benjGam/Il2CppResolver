using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Navigation;

/// <summary>
/// Binds resolved public entities to the internal navigation session without exposing resolution infrastructure through the public API.
/// The binding also owns the monotonically increasing cache generation stamped onto every resolved entity.
/// </summary>
internal sealed class ResolutionBinding : IResolutionNavigator
{
    /// <summary>Stores the session navigator after the resolver stack has completed construction.</summary>
    private IResolutionNavigator? _navigator;

    /// <summary>Stores the current cache generation used to stamp newly resolved runtime entities.</summary>
    private long _generation;

    /// <summary>Gets the current cache generation used by newly materialized resolved entities.</summary>
    public long Generation => _generation;

    /// <summary>Binds this proxy exactly once to the fully constructed session navigator.</summary>
    /// <param name="navigator">The session navigator that will service resolved-entity endpoints.</param>
    /// <exception cref="InvalidOperationException">Thrown when the binding has already been initialized.</exception>
    public void Bind(IResolutionNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);

        if (_navigator is not null)
            throw new InvalidOperationException("The resolution binding is already attached to a navigator.");

        _navigator = navigator;
    }

    /// <summary>Advances the cache generation so previously resolved entities can no longer navigate through stale runtime pointers.</summary>
    public void AdvanceGeneration()
    {
        _generation = checked(_generation + 1);
    }

    /// <summary>Forwards assembly type enumeration to the attached session navigator.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <param name="generation">The cache generation that produced the assembly.</param>
    /// <returns>The resolved types exposed by the assembly image.</returns>
    public IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly, long generation) => GetNavigator().GetTypes(assembly, generation);

    /// <summary>Forwards targeted type resolution relative to an already resolved assembly.</summary>
    /// <param name="assembly">The resolved assembly containing the requested type.</param>
    /// <param name="namespaceName">The exact managed namespace.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <param name="generation">The cache generation that produced the assembly.</param>
    /// <returns>The resolved runtime type.</returns>
    public ResolvedType ResolveType(ResolvedAssembly assembly, string namespaceName, string typeName, long generation) => GetNavigator().ResolveType(assembly, namespaceName, typeName, generation);

    /// <summary>Forwards complete method enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every method declared by the type.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, long generation) => GetNavigator().GetMethods(type, generation);

    /// <summary>Forwards overload enumeration for one exact method name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every matching method overload.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name, long generation) => GetNavigator().GetMethods(type, name, generation);

    /// <summary>Forwards targeted exact-overload resolution relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    public ResolvedMethod ResolveMethod(ResolvedType type, string methodName, IReadOnlyList<string> parameterTypeNames, long generation) => GetNavigator().ResolveMethod(type, methodName, parameterTypeNames, generation);

    /// <summary>Forwards complete field enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every field declared by the type.</returns>
    public IReadOnlyList<ResolvedField> GetFields(ResolvedType type, long generation) => GetNavigator().GetFields(type, generation);

    /// <summary>Forwards targeted field resolution relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    public ResolvedField ResolveField(ResolvedType type, string fieldName, long generation) => GetNavigator().ResolveField(type, fieldName, generation);

    /// <summary>Forwards native method-code mapping through the session's active MethodInfo layout policy.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="generation">The cache generation that produced the method.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, long generation) => GetNavigator().ResolveMethodCode(method, generation);

    /// <summary>Forwards native method-code mapping through one explicit MethodInfo layout override.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <param name="generation">The cache generation that produced the method.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout, long generation) => GetNavigator().ResolveMethodCode(method, layout, generation);

    /// <summary>Forwards static-field storage mapping through the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, long generation) => GetNavigator().ResolveFieldStorage(field, generation);

    /// <summary>Forwards static-field storage mapping through one explicit Il2CppClass layout override.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout, long generation) => GetNavigator().ResolveFieldStorage(field, layout, generation);

    /// <summary>Gets the attached navigator or rejects navigation before session construction has completed.</summary>
    /// <returns>The fully initialized session navigator.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no navigator has been attached yet.</exception>
    private IResolutionNavigator GetNavigator()
    {
        return _navigator ?? throw new InvalidOperationException("The resolution binding has not been attached to a session navigator.");
    }
}
