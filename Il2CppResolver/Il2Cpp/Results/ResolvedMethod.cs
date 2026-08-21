using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents a managed method resolved to a live IL2CPP <c>MethodInfo</c> and bound to the resolver session that produced it.
/// The result contains the verified semantic signature while native-code mapping remains an explicit navigation step governed by the session's compatibility configuration.
/// </summary>
public sealed class ResolvedMethod
{
    /// <summary>Provides session-bound navigation to native method-code mapping.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this MethodInfo identity was resolved.</summary>
    private readonly long _generation;

    /// <summary>Gets the semantic method identity associated with this resolved runtime entity.</summary>
    public MethodQuery Query { get; }
    /// <summary>Gets the resolved runtime type declaring this method.</summary>
    public ResolvedType DeclaringType { get; }
    /// <summary>Gets the native address of the resolved IL2CPP <c>MethodInfo</c>.</summary>
    public nint MethodInfoAddress { get; }
    /// <summary>Gets the semantic return type name reported by IL2CPP.</summary>
    public string ReturnTypeName { get; }
    /// <summary>Gets the ordered semantic parameter type names verified for this method.</summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>Initializes an immutable session-bound method result.</summary>
    /// <param name="query">The semantic method identity associated with the result.</param>
    /// <param name="declaringType">The resolved runtime declaring type.</param>
    /// <param name="methodInfoAddress">The native <c>MethodInfo*</c> address.</param>
    /// <param name="returnTypeName">The semantic return type name reported by IL2CPP.</param>
    /// <param name="parameterTypeNames">The verified ordered parameter type names.</param>
    /// <param name="navigator">The internal session navigator servicing native-code mapping.</param>
    /// <param name="generation">The cache generation that produced this runtime identity.</param>
    internal ResolvedMethod(MethodQuery query, ResolvedType declaringType, nint methodInfoAddress, string returnTypeName, IReadOnlyList<string> parameterTypeNames, IResolutionNavigator navigator, long generation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnTypeName);
        ArgumentNullException.ThrowIfNull(parameterTypeNames);
        ArgumentNullException.ThrowIfNull(navigator);

        if (methodInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodInfoAddress), "The resolved IL2CPP MethodInfo address cannot be zero.");

        Query = query;
        DeclaringType = declaringType;
        MethodInfoAddress = methodInfoAddress;
        ReturnTypeName = returnTypeName;
        ParameterTypeNames = Array.AsReadOnly(parameterTypeNames.ToArray());
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>Maps this method to validated direct native code using the MethodInfo layout selected or automatically detected by the owning session.</summary>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveCode()
    {
        return _navigator.ResolveMethodCode(this, _generation);
    }

    /// <summary>Maps this method to validated direct native code using one explicit MethodInfo layout override without changing session configuration.</summary>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveCode(Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return _navigator.ResolveMethodCode(this, layout, _generation);
    }
}
