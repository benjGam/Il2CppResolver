using UnityIl2CppResolver.Il2Cpp.Queries;

namespace UnityIl2CppResolver.Il2Cpp.Resolution.Model;

/// <summary>
/// Represents a managed method successfully resolved to a live IL2CPP <c>MethodInfo</c>.
/// The result contains the verified semantic signature and runtime method identity but deliberately excludes the compiled native method address, which belongs to a separate version-sensitive native mapping stage.
/// </summary>
public sealed record ResolvedMethod
{
    /// <summary>
    /// Gets the semantic method query that produced this resolution result.
    /// </summary>
    public MethodQuery Query { get; }

    /// <summary>
    /// Gets the resolved runtime type declaring this method.
    /// </summary>
    public ResolvedType DeclaringType { get; }

    /// <summary>
    /// Gets the native address of the resolved IL2CPP <c>MethodInfo</c>.
    /// </summary>
    public nint MethodInfoAddress { get; }

    /// <summary>
    /// Gets the semantic return type name reported by IL2CPP.
    /// </summary>
    public string ReturnTypeName { get; }

    /// <summary>
    /// Gets the ordered semantic parameter type names verified during method resolution.
    /// </summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>
    /// Initializes the immutable result of a successful semantic method resolution.
    /// </summary>
    /// <param name="query">The semantic method query that was resolved.</param>
    /// <param name="declaringType">The resolved runtime declaring type.</param>
    /// <param name="methodInfoAddress">The native <c>MethodInfo*</c> address.</param>
    /// <param name="returnTypeName">The semantic return type name reported by IL2CPP.</param>
    /// <param name="parameterTypeNames">The verified ordered parameter type names.</param>
    internal ResolvedMethod(MethodQuery query, ResolvedType declaringType, nint methodInfoAddress, string returnTypeName, IReadOnlyList<string> parameterTypeNames)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnTypeName);
        ArgumentNullException.ThrowIfNull(parameterTypeNames);

        if (methodInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodInfoAddress), "The resolved IL2CPP MethodInfo address cannot be zero.");

        Query = query;
        DeclaringType = declaringType;
        MethodInfoAddress = methodInfoAddress;
        ReturnTypeName = returnTypeName;
        ParameterTypeNames = Array.AsReadOnly(parameterTypeNames.ToArray());
    }
}