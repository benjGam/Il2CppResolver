using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using UnityIl2CppResolver.Il2Cpp.Runtime.Model;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Implements semantic IL2CPP resolution through live runtime APIs while delegating expensive domain, assembly and class-member discovery to a session-scoped local runtime catalogue.
/// Only successful semantic results are stored in <see cref="ResolutionCache"/>, while runtime snapshots remain independently invalidatable through the owning session.
/// </summary>
internal sealed class RuntimeResolutionBackend : IIl2CppResolutionBackend
{
    /// <summary>Provides direct IL2CPP runtime operations required for class resolution.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Provides cached runtime domain, assembly and class-member snapshots.</summary>
    private readonly Il2CppRuntimeCatalog _catalog;
    /// <summary>Stores successful semantic resolution results.</summary>
    private readonly ResolutionCache _cache;
    /// <summary>Defines the timeout applied to individual runtime calls not absorbed by the catalogue.</summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Initializes runtime-backed semantic resolution.
    /// </summary>
    /// <param name="runtime">The live IL2CPP runtime.</param>
    /// <param name="catalog">The session-scoped runtime catalogue.</param>
    /// <param name="cache">The session-scoped semantic resolution cache.</param>
    /// <param name="callTimeout">The timeout applied to each individual runtime invocation.</param>
    public RuntimeResolutionBackend(Il2CppRuntime runtime, Il2CppRuntimeCatalog catalog, ResolutionCache cache, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(cache);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _catalog = catalog;
        _cache = cache;
        _callTimeout = callTimeout;
    }

    /// <summary>Resolves a loaded assembly from its semantic name using the local runtime assembly snapshot.</summary>
    /// <param name="query">The semantic assembly query.</param>
    /// <returns>The resolved runtime assembly.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetAssembly(query, out ResolvedAssembly? cachedAssembly))
            return cachedAssembly;

        Il2CppAssemblyInfo match = _catalog.ResolveAssembly(query.Name);
        ResolvedAssembly result = new(query, match.Name, match.AssemblyAddress, match.ImageAddress);
        _cache.StoreAssembly(result);
        return result;
    }

    /// <summary>Resolves a managed type through <c>il2cpp_class_from_name</c> after resolving its containing assembly locally.</summary>
    /// <param name="query">The complete semantic type identity.</param>
    /// <returns>The resolved runtime type.</returns>
    public ResolvedType ResolveType(TypeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetType(query, out ResolvedType? cachedType))
            return cachedType;

        ResolvedAssembly assembly = ResolveAssembly(query.Assembly);
        nint classAddress = _runtime.GetClass(assembly.ImageAddress, query.Namespace, query.Name, _callTimeout);

        if (classAddress == 0)
            throw new KeyNotFoundException($"IL2CPP type '{query.Namespace}.{query.Name}' was not found in assembly '{assembly.Name}'.");

        ResolvedType result = new(query, assembly, classAddress);
        _cache.StoreType(result);
        return result;
    }

    /// <summary>
    /// Resolves a managed method by exact name and ordered parameter type names.
    /// Method enumeration and name inspection occur once per declaring class snapshot, and complete signatures are inspected only for name-matching candidates.
    /// </summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The unique runtime method matching the requested semantic signature.</returns>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetMethod(query, out ResolvedMethod? cachedMethod))
            return cachedMethod;

        ResolvedType declaringType = ResolveType(query.DeclaringType);
        Il2CppClassMemberCatalog members = _catalog.GetClassMembers(declaringType.ClassAddress);
        IReadOnlyList<nint> candidates = members.FindMethods(query.Name);
        Il2CppMethodInfo? match = null;

        foreach (nint methodAddress in candidates)
        {
            Il2CppMethodInfo method = members.GetMethodInfo(methodAddress);

            if (!ParametersMatch(method.ParameterTypeNames, query.ParameterTypeNames))
                continue;

            if (match is not null)
                throw new InvalidDataException($"Multiple IL2CPP methods match semantic signature '{FormatMethodSignature(query)}'.");

            match = method;
        }

        if (match is null)
            throw new KeyNotFoundException($"IL2CPP method '{FormatMethodSignature(query)}' was not found.");

        ResolvedMethod result = new(query, declaringType, match.MethodAddress, match.ReturnTypeName, match.ParameterTypeNames);
        _cache.StoreMethod(result);
        return result;
    }

    /// <summary>
    /// Resolves a managed field by exact declaring type and field name.
    /// Field enumeration and name inspection occur once per declaring class snapshot.
    /// </summary>
    /// <param name="query">The semantic field query to resolve.</param>
    /// <returns>The unique runtime field matching the requested identity.</returns>
    public ResolvedField ResolveField(FieldQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetField(query, out ResolvedField? cachedField))
            return cachedField;

        ResolvedType declaringType = ResolveType(query.DeclaringType);
        Il2CppClassMemberCatalog members = _catalog.GetClassMembers(declaringType.ClassAddress);
        IReadOnlyList<nint> candidates = members.FindFields(query.Name);
        Il2CppFieldInfo? match = null;

        foreach (nint fieldAddress in candidates)
        {
            Il2CppFieldInfo field = members.GetFieldInfo(fieldAddress);

            if (match is not null)
                throw new InvalidDataException($"Multiple IL2CPP fields named '{query.Name}' were found on declaring type '{query.DeclaringType.Namespace}.{query.DeclaringType.Name}'.");

            match = field;
        }

        if (match is null)
            throw new KeyNotFoundException($"IL2CPP field '{query.DeclaringType.Namespace}.{query.DeclaringType.Name}.{query.Name}' was not found.");

        FieldStorageKind storageKind;
        nuint? instanceOffset = null;
        nuint? staticStorageOffset = null;

        if (match.IsLiteral)
            storageKind = FieldStorageKind.Literal;
        else if (match.IsThreadStatic)
            storageKind = FieldStorageKind.ThreadStatic;
        else if (match.IsStatic)
        {
            storageKind = FieldStorageKind.Static;
            staticStorageOffset = match.Offset;
        }
        else
        {
            storageKind = FieldStorageKind.Instance;
            instanceOffset = match.Offset;
        }

        ResolvedField result = new(query, declaringType, match.FieldAddress, match.TypeName, match.Attributes, storageKind, instanceOffset, staticStorageOffset);
        _cache.StoreField(result);
        return result;
    }

    /// <summary>Determines whether two ordered parameter type sequences are semantically identical.</summary>
    /// <param name="runtimeParameters">The parameter types reported by IL2CPP.</param>
    /// <param name="requestedParameters">The parameter types requested by the query.</param>
    /// <returns><see langword="true"/> when both sequences contain the same names in the same order.</returns>
    private static bool ParametersMatch(IReadOnlyList<string> runtimeParameters, IReadOnlyList<string> requestedParameters)
    {
        if (runtimeParameters.Count != requestedParameters.Count)
            return false;

        for (int index = 0; index < runtimeParameters.Count; index++)
        {
            if (!string.Equals(runtimeParameters[index], requestedParameters[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>Formats a semantic method query into a diagnostic signature.</summary>
    /// <param name="query">The query to format.</param>
    /// <returns>The fully qualified diagnostic signature.</returns>
    private static string FormatMethodSignature(MethodQuery query)
    {
        return $"{query.DeclaringType.Namespace}.{query.DeclaringType.Name}.{query.Name}({string.Join(", ", query.ParameterTypeNames)})";
    }
}
