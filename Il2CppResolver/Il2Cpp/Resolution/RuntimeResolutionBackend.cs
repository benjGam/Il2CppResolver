using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using RuntimeAssemblyInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppAssemblyInfo;
using RuntimeClassInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassInfo;
using RuntimeFieldInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppFieldInfo;
using RuntimeMethodInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodInfo;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Implements semantic IL2CPP resolution and explicit runtime navigation through live public APIs while delegating expensive domain, assembly, image and class-member discovery to session-scoped local catalogues.
/// Targeted resolution remains independent from full enumeration, and an identity map ensures the same native runtime entity is represented by one public object within a cache generation.
/// </summary>
internal sealed class RuntimeResolutionBackend : IIl2CppResolutionBackend
{
    /// <summary>Provides direct IL2CPP runtime operations required for targeted class resolution.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Provides cached runtime domain, assembly, image-type and class-member snapshots.</summary>
    private readonly Il2CppRuntimeCatalog _catalog;
    /// <summary>Stores successful semantic results and native-identity mappings.</summary>
    private readonly ResolutionCache _cache;
    /// <summary>Binds newly materialized public results to the owning session navigator and current cache generation.</summary>
    private readonly ResolutionBinding _binding;
    /// <summary>Defines the timeout applied to individual runtime calls not absorbed by local catalogues.</summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>Initializes runtime-backed semantic resolution and navigation.</summary>
    /// <param name="runtime">The live IL2CPP runtime.</param>
    /// <param name="catalog">The session-scoped runtime catalogue.</param>
    /// <param name="cache">The session-scoped semantic and identity cache.</param>
    /// <param name="binding">The session navigation binding stamped onto resolved public entities.</param>
    /// <param name="callTimeout">The timeout applied to each individual runtime invocation.</param>
    public RuntimeResolutionBackend(Il2CppRuntime runtime, Il2CppRuntimeCatalog catalog, ResolutionCache cache, ResolutionBinding binding, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(binding);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _catalog = catalog;
        _cache = cache;
        _binding = binding;
        _callTimeout = callTimeout;
    }

    /// <summary>Gets every loaded assembly from the session-scoped runtime snapshot.</summary>
    /// <returns>Every resolved assembly registered in the active runtime domain.</returns>
    public IReadOnlyList<ResolvedAssembly> GetAssemblies()
    {
        IReadOnlyList<RuntimeAssemblyInfo> assemblies = _catalog.GetAssemblies();
        List<ResolvedAssembly> results = new(assemblies.Count);

        foreach (RuntimeAssemblyInfo assembly in assemblies)
        {
            AssemblyQuery query = new(assembly.Name);
            results.Add(GetOrCreateAssembly(query, assembly));
        }

        return results.AsReadOnly();
    }

    /// <summary>Resolves a loaded assembly from its semantic name using the local runtime assembly snapshot.</summary>
    /// <param name="query">The semantic assembly query.</param>
    /// <returns>The resolved runtime assembly.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_cache.TryGetAssembly(query, out ResolvedAssembly? cachedAssembly))
            return cachedAssembly;

        RuntimeAssemblyInfo assembly = _catalog.ResolveAssembly(query.Name);
        return GetOrCreateAssembly(query, assembly);
    }

    /// <summary>Gets every type exposed by an already resolved assembly image through the optional image-class enumeration capability.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <returns>Every resolved runtime type exposed by the image.</returns>
    public IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        IReadOnlyList<RuntimeClassInfo> classes = _catalog.GetImageTypes(assembly.ImageAddress).GetTypes();
        List<ResolvedType> results = new(classes.Count);

        foreach (RuntimeClassInfo runtimeType in classes)
        {
            TypeQuery query = new(assembly.Name, runtimeType.Namespace, runtimeType.Name);
            results.Add(GetOrCreateType(query, assembly, runtimeType.ClassAddress));
        }

        return results.AsReadOnly();
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

        return GetOrCreateType(query, assembly, classAddress);
    }

    /// <summary>Gets every method declared by an already resolved type and materializes complete semantic signatures.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <returns>Every declared resolved method.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Il2CppClassMemberCatalog members = _catalog.GetClassMembers(type.ClassAddress);
        IReadOnlyList<RuntimeMethodInfo> methods = members.GetMethods();
        List<ResolvedMethod> results = new(methods.Count);

        foreach (RuntimeMethodInfo method in methods)
            results.Add(GetOrCreateMethod(CreateMethodQuery(type, method), type, method));

        return results.AsReadOnly();
    }

    /// <summary>Gets every overload declared by a type with the exact requested method name while inspecting only matching candidate signatures.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <returns>Every resolved overload sharing the requested name.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Il2CppClassMemberCatalog members = _catalog.GetClassMembers(type.ClassAddress);
        IReadOnlyList<RuntimeMethodInfo> methods = members.GetMethods(name);
        List<ResolvedMethod> results = new(methods.Count);

        foreach (RuntimeMethodInfo method in methods)
            results.Add(GetOrCreateMethod(CreateMethodQuery(type, method), type, method));

        return results.AsReadOnly();
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
        RuntimeMethodInfo? match = null;

        foreach (nint methodAddress in candidates)
        {
            RuntimeMethodInfo method = members.GetMethodInfo(methodAddress);

            if (!ParametersMatch(method.ParameterTypeNames, query.ParameterTypeNames))
                continue;

            if (match is not null)
                throw new InvalidDataException($"Multiple IL2CPP methods match semantic signature '{FormatMethodSignature(query)}'.");

            match = method;
        }

        if (match is null)
            throw new KeyNotFoundException($"IL2CPP method '{FormatMethodSignature(query)}' was not found.");

        return GetOrCreateMethod(query, declaringType, match);
    }

    /// <summary>Gets every field declared by an already resolved type and materializes complete semantic field descriptions.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <returns>Every declared resolved field.</returns>
    public IReadOnlyList<ResolvedField> GetFields(ResolvedType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Il2CppClassMemberCatalog members = _catalog.GetClassMembers(type.ClassAddress);
        IReadOnlyList<RuntimeFieldInfo> fields = members.GetFields();
        List<ResolvedField> results = new(fields.Count);

        foreach (RuntimeFieldInfo field in fields)
        {
            FieldQuery query = new(type.Query, field.Name);
            results.Add(GetOrCreateField(query, type, field));
        }

        return results.AsReadOnly();
    }

    /// <summary>Resolves a managed field by exact declaring type and field name.</summary>
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
        RuntimeFieldInfo? match = null;

        foreach (nint fieldAddress in candidates)
        {
            RuntimeFieldInfo field = members.GetFieldInfo(fieldAddress);

            if (match is not null)
                throw new InvalidDataException($"Multiple IL2CPP fields named '{query.Name}' were found on declaring type '{query.DeclaringType.Namespace}.{query.DeclaringType.Name}'.");

            match = field;
        }

        if (match is null)
            throw new KeyNotFoundException($"IL2CPP field '{query.DeclaringType.Namespace}.{query.DeclaringType.Name}.{query.Name}' was not found.");

        return GetOrCreateField(query, declaringType, match);
    }

    /// <summary>Gets or creates the public assembly object associated with one native image identity and caches the supplied semantic alias.</summary>
    /// <param name="query">The semantic assembly identity associated with this access path.</param>
    /// <param name="assembly">The runtime assembly description.</param>
    /// <returns>The identity-mapped public assembly object.</returns>
    private ResolvedAssembly GetOrCreateAssembly(AssemblyQuery query, RuntimeAssemblyInfo assembly)
    {
        if (_cache.TryGetAssemblyByImageAddress(assembly.ImageAddress, out ResolvedAssembly? existing))
        {
            _cache.CacheAssemblyQuery(query, existing);
            return existing;
        }

        ResolvedAssembly result = new(query, assembly.Name, assembly.AssemblyAddress, assembly.ImageAddress, _binding, _binding.Generation);
        _cache.StoreAssembly(result);
        return result;
    }

    /// <summary>Gets or creates the public type object associated with one native class identity and caches the supplied semantic alias.</summary>
    /// <param name="query">The semantic type identity associated with this access path.</param>
    /// <param name="assembly">The resolved containing assembly.</param>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> identity.</param>
    /// <returns>The identity-mapped public type object.</returns>
    private ResolvedType GetOrCreateType(TypeQuery query, ResolvedAssembly assembly, nint classAddress)
    {
        if (_cache.TryGetTypeByClassAddress(classAddress, out ResolvedType? existing))
        {
            _cache.CacheTypeQuery(query, existing);
            return existing;
        }

        ResolvedType result = new(query, assembly, classAddress, _binding, _binding.Generation);
        _cache.StoreType(result);
        return result;
    }

    /// <summary>Gets or creates the public method object associated with one native MethodInfo identity and caches the supplied semantic alias.</summary>
    /// <param name="query">The semantic method identity associated with this access path.</param>
    /// <param name="declaringType">The resolved declaring type.</param>
    /// <param name="method">The complete runtime method description.</param>
    /// <returns>The identity-mapped public method object.</returns>
    private ResolvedMethod GetOrCreateMethod(MethodQuery query, ResolvedType declaringType, RuntimeMethodInfo method)
    {
        if (_cache.TryGetMethodByAddress(method.MethodAddress, out ResolvedMethod? existing))
        {
            _cache.CacheMethodQuery(query, existing);
            return existing;
        }

        ResolvedMethod result = new(query, declaringType, method.MethodAddress, method.ReturnTypeName, method.ParameterTypeNames, _binding, _binding.Generation);
        _cache.StoreMethod(result);
        return result;
    }

    /// <summary>Gets or creates the public field object associated with one native FieldInfo identity and caches the supplied semantic alias.</summary>
    /// <param name="query">The semantic field identity associated with this access path.</param>
    /// <param name="declaringType">The resolved declaring type.</param>
    /// <param name="field">The complete runtime field description.</param>
    /// <returns>The identity-mapped public field object.</returns>
    private ResolvedField GetOrCreateField(FieldQuery query, ResolvedType declaringType, RuntimeFieldInfo field)
    {
        if (_cache.TryGetFieldByAddress(field.FieldAddress, out ResolvedField? existing))
        {
            _cache.CacheFieldQuery(query, existing);
            return existing;
        }

        GetFieldStorage(field, out FieldStorageKind storageKind, out nuint? instanceOffset, out nuint? staticStorageOffset);
        ResolvedField result = new(query, declaringType, field.FieldAddress, field.TypeName, field.Attributes, storageKind, instanceOffset, staticStorageOffset, _binding, _binding.Generation);
        _cache.StoreField(result);
        return result;
    }

    /// <summary>Creates a canonical semantic method query from an already resolved declaring type and runtime signature.</summary>
    /// <param name="declaringType">The resolved declaring type.</param>
    /// <param name="method">The complete runtime method description.</param>
    /// <returns>The semantic method identity represented by the runtime description.</returns>
    private static MethodQuery CreateMethodQuery(ResolvedType declaringType, RuntimeMethodInfo method)
    {
        return new MethodQuery(declaringType.Query, method.Name, method.ParameterTypeNames.ToArray());
    }

    /// <summary>Translates runtime field flags and raw offset into the public storage-kind model.</summary>
    /// <param name="field">The complete runtime field description.</param>
    /// <param name="storageKind">Receives the public storage category.</param>
    /// <param name="instanceOffset">Receives the object-relative offset for instance fields.</param>
    /// <param name="staticStorageOffset">Receives the class-static-data-relative offset for normal static fields.</param>
    private static void GetFieldStorage(RuntimeFieldInfo field, out FieldStorageKind storageKind, out nuint? instanceOffset, out nuint? staticStorageOffset)
    {
        instanceOffset = null;
        staticStorageOffset = null;

        if (field.IsLiteral)
            storageKind = FieldStorageKind.Literal;
        else if (field.IsThreadStatic)
            storageKind = FieldStorageKind.ThreadStatic;
        else if (field.IsStatic)
        {
            storageKind = FieldStorageKind.Static;
            staticStorageOffset = field.Offset;
        }
        else
        {
            storageKind = FieldStorageKind.Instance;
            instanceOffset = field.Offset;
        }
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
