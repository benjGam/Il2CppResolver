using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;
using UnityIl2CppResolver.Il2Cpp.Runtime;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Resolves semantic IL2CPP entities by interrogating the public native runtime API exposed by the target process.
/// This backend converts assembly and type queries into live <c>Il2CppAssembly</c>, <c>Il2CppImage</c> and <c>Il2CppClass</c> entities without relying on external metadata files, hardcoded RVAs or native code signatures.
/// It currently performs uncached runtime resolution; session-level caching can be introduced later without changing the semantic query or result models.
/// </summary>
internal sealed class RuntimeResolutionBackend : IIl2CppResolutionBackend
{
    /// <summary>
    /// Represents the low-level IL2CPP runtime abstraction used to enumerate assemblies and resolve classes.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Stores semantic entities already resolved during the current target session.
    /// The backend consults this cache before performing live IL2CPP traversal and records only successfully validated results.
    /// </summary>
    private readonly ResolutionCache _cache;

    /// <summary>
    /// Defines the maximum duration allowed for each individual remote IL2CPP API invocation performed by this backend.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Initializes semantic resolution over the specified IL2CPP runtime and session-scoped resolution cache.
    /// </summary>
    /// <param name="runtime">The low-level IL2CPP runtime abstraction associated with the target process.</param>
    /// <param name="cache">The session-scoped cache used to reuse successful semantic resolution results.</param>
    /// <param name="callTimeout">The finite timeout applied independently to each native runtime call.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="runtime"/> or <paramref name="cache"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="callTimeout"/> is zero or negative.
    /// </exception>
    public RuntimeResolutionBackend(Il2CppRuntime runtime, ResolutionCache cache, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(cache);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _cache = cache;
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Resolves an assembly by semantic name through the active IL2CPP domain.
    /// Conventional <c>.dll</c> and <c>.exe</c> suffixes are ignored during comparison so simple managed assembly names and runtime image names resolve to the same entity.
    /// </summary>
    /// <param name="query">The semantic assembly query to resolve.</param>
    /// <returns>The concrete runtime assembly and image associated with the requested semantic identity.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no loaded IL2CPP assembly matches the requested name.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when multiple loaded runtime assemblies unexpectedly match the same normalized semantic name.
    /// </exception>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetAssembly(query, out ResolvedAssembly? cachedAssembly))
            return cachedAssembly;

        nint domain = _runtime.GetDomain(_callTimeout);
        IReadOnlyList<Il2CppAssemblyInfo> assemblies = _runtime.GetAssemblyInfos(domain, _callTimeout);
        string requestedName = NormalizeAssemblyName(query.Name);

        Il2CppAssemblyInfo? match = null;

        foreach (Il2CppAssemblyInfo assembly in assemblies)
        {
            string candidateName = NormalizeAssemblyName(assembly.Name);

            if (!string.Equals(candidateName, requestedName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match is not null)
                throw new InvalidDataException($"Multiple IL2CPP assemblies match semantic assembly name '{query.Name}'.");

            match = assembly;
        }

        if (match is null)
            throw new KeyNotFoundException($"IL2CPP assembly '{query.Name}' was not found in the active runtime domain.");

        ResolvedAssembly result = new(query, match.Name, match.AssemblyAddress, match.ImageAddress);

        _cache.StoreAssembly(result);

        return result;
    }

    /// <summary>
    /// Resolves a managed type by first locating its containing assembly and then invoking <c>il2cpp_class_from_name</c> against the corresponding runtime image.
    /// </summary>
    /// <param name="query">The complete semantic assembly, namespace and type identity to resolve.</param>
    /// <returns>The concrete runtime <c>Il2CppClass</c> associated with the requested managed type.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when either the containing assembly or requested type cannot be found in the active IL2CPP runtime.
    /// </exception>
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
    /// Resolves a managed method by declaring type, method name and ordered parameter type names.
    /// Candidate methods are first filtered by name before their complete signatures are inspected so unnecessary IL2CPP type-name allocations and remote calls are avoided.
    /// </summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The unique runtime <c>MethodInfo</c> whose semantic signature matches the query.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no declared method matches the requested semantic signature.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when multiple runtime methods match the same requested signature.
    /// </exception>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetMethod(query, out ResolvedMethod? cachedMethod))
            return cachedMethod;

        ResolvedType declaringType = ResolveType(query.DeclaringType);
        IReadOnlyList<nint> methods = _runtime.GetMethods(declaringType.ClassAddress, _callTimeout);
        Il2CppMethodInfo? match = null;

        foreach (nint methodAddress in methods)
        {
            string methodName = _runtime.GetMethodName(methodAddress, _callTimeout);

            if (!string.Equals(methodName, query.Name, StringComparison.Ordinal))
                continue;

            Il2CppMethodInfo method = _runtime.GetMethodInfo(methodAddress, _callTimeout);

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
    /// Determines whether two ordered semantic parameter type sequences identify the same runtime method signature.
    /// </summary>
    /// <param name="runtimeParameters">The parameter types reported by the IL2CPP runtime.</param>
    /// <param name="requestedParameters">The parameter types requested by the semantic query.</param>
    /// <returns><see langword="true"/> when both parameter lists contain the same type names in the same order; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Formats a semantic method query into a compact diagnostic signature.
    /// </summary>
    /// <param name="query">The method query to format.</param>
    /// <returns>A readable fully qualified method signature suitable for diagnostics.</returns>
    private static string FormatMethodSignature(MethodQuery query)
    {
        string parameters = string.Join(", ", query.ParameterTypeNames);
        return $"{query.DeclaringType.Namespace}.{query.DeclaringType.Name}.{query.Name}({parameters})";
    }

    /// <summary>
    /// Normalizes an assembly identifier for semantic comparison by removing conventional managed image suffixes while preserving the remaining assembly identity.
    /// </summary>
    /// <param name="name">The simple assembly name or runtime image name to normalize.</param>
    /// <returns>The normalized assembly identity without a trailing <c>.dll</c> or <c>.exe</c> suffix.</returns>
    private static string NormalizeAssemblyName(string name)
    {
        string normalized = name.Trim();

        if (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return normalized[..^4];

        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return normalized[..^4];

        return normalized;
    }

    /// <summary>
    /// Resolves a managed field by exact declaring type and field name.
    /// Candidate fields are enumerated from the declaring IL2CPP class and the matching field is inspected to determine its semantic type, metadata attributes and storage category.
    /// </summary>
    /// <param name="query">The semantic field query to resolve.</param>
    /// <returns>The unique runtime field matching the requested declaring type and name.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the requested field does not exist on the declaring runtime type.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when multiple fields unexpectedly match the same name.
    /// </exception>
    public ResolvedField ResolveField(FieldQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetField(query, out ResolvedField? cachedField))
            return cachedField;

        ResolvedType declaringType = ResolveType(query.DeclaringType);
        IReadOnlyList<nint> fields = _runtime.GetFields(declaringType.ClassAddress, _callTimeout);
        Il2CppFieldInfo? match = null;

        foreach (nint fieldAddress in fields)
        {
            string fieldName = _runtime.GetFieldName(fieldAddress, _callTimeout);

            if (!string.Equals(fieldName, query.Name, StringComparison.Ordinal))
                continue;

            Il2CppFieldInfo field = _runtime.GetFieldInfo(fieldAddress, _callTimeout);

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
        {
            storageKind = FieldStorageKind.Literal;
        }
        else if (match.IsThreadStatic)
        {
            storageKind = FieldStorageKind.ThreadStatic;
        }
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
}