using UnityIl2CppResolver.Il2Cpp.Detection;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Mapping;
using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using UnityIl2CppResolver.Il2Cpp.Values;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeMethodMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodMetadata;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Owns the complete native and IL2CPP state associated with one resolver attachment to a target process.
/// The session serializes public resolution and configuration operations, owns all session-scoped caches and runtime catalogues, and separates explicitly selected compatibility layouts from layouts inferred automatically from runtime evidence.
/// </summary>
internal sealed class ResolutionSession : IDisposable, IResolutionNavigator
{
    /// <summary>Defines the maximum number of methods sampled when MethodInfo layout detection is required.</summary>
    private const int MethodInfoLayoutSampleSize = 12;
    /// <summary>Defines the minimum number of executable method pointers required to accept an automatically detected MethodInfo layout.</summary>
    private const int MinimumValidatedMethodCount = 5;
    /// <summary>Defines the minimum number of independent declaring classes required to accept an automatically detected Il2CppClass layout.</summary>
    private const int MinimumValidatedClassCount = 3;

    /// <summary>Serializes session state transitions, cache access and remote resolution operations into one coherent execution flow.</summary>
    private readonly object _syncRoot = new();
    /// <summary>Owns the native process handle for the complete lifetime of the session.</summary>
    private readonly TargetProcess _process;
    /// <summary>Represents the validated IL2CPP target discovered inside the owned process.</summary>
    private readonly Il2CppTarget _target;
    /// <summary>Provides low-level semantic access to the live IL2CPP runtime.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Maintains expensive domain, assembly and class-member snapshots for the current cache generation.</summary>
    private readonly Il2CppRuntimeCatalog _runtimeCatalog;
    /// <summary>Stores semantic and native resolution results for the current cache generation.</summary>
    private readonly ResolutionCache _cache;
    /// <summary>Binds public resolved entities back to this session and tracks cache-generation invalidation.</summary>
    private readonly ResolutionBinding _binding;
    /// <summary>Represents the active semantic resolution backend.</summary>
    private readonly IIl2CppResolutionBackend _backend;
    /// <summary>Stores built-in and consumer-registered layout candidates available to automatic detection.</summary>
    private readonly Il2CppLayoutRegistry _layoutRegistry;
    /// <summary>Resolves static-field storage through optional public IL2CPP runtime APIs when available.</summary>
    private readonly Il2CppRuntimeStaticFieldStorageResolver _runtimeStaticFieldStorageResolver;
    /// <summary>Reads validated scalar, enum and managed-reference field values from concrete target storage.</summary>
    private readonly Il2CppFieldValueReader _fieldValueReader;
    /// <summary>Defines the maximum duration allowed for each individual remote IL2CPP runtime invocation.</summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>Stores the MethodInfo layout explicitly selected by the consumer for default method-code resolution.</summary>
    private Il2CppMethodInfoLayout? _selectedMethodInfoLayout;
    /// <summary>Stores the Il2CppClass layout explicitly selected by the consumer for default static-field storage resolution.</summary>
    private Il2CppClassLayout? _selectedFieldStorageLayout;
    /// <summary>Stores the MethodInfo layout inferred automatically for the current cache generation.</summary>
    private Il2CppMethodInfoLayout? _detectedMethodInfoLayout;
    /// <summary>Stores the Il2CppClass layout inferred automatically for the current cache generation.</summary>
    private Il2CppClassLayout? _detectedFieldStorageLayout;
    /// <summary>Indicates whether this session has released ownership of its target process.</summary>
    private bool _disposed;

    /// <summary>Gets the operating system identifier of the process owned by this session.</summary>
    public int ProcessId => _process.ProcessId;

    /// <summary>Gets the MethodInfo layout explicitly selected for default method-code resolution, or <see langword="null"/> when automatic selection is active.</summary>
    public Il2CppMethodInfoLayout? SelectedMethodInfoLayout
    {
        get
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                return _selectedMethodInfoLayout;
            }
        }
    }

    /// <summary>Gets the Il2CppClass layout explicitly selected for default field-storage resolution, or <see langword="null"/> when automatic selection is active.</summary>
    public Il2CppClassLayout? SelectedFieldStorageLayout
    {
        get
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                return _selectedFieldStorageLayout;
            }
        }
    }

    /// <summary>
    /// Initializes a fully constructed resolution session from validated native, runtime, catalogue and configuration components.
    /// </summary>
    /// <param name="process">The owned target process.</param>
    /// <param name="target">The validated IL2CPP target.</param>
    /// <param name="runtime">The live runtime access layer.</param>
    /// <param name="runtimeCatalog">The session-scoped runtime catalogue.</param>
    /// <param name="backend">The semantic resolution backend.</param>
    /// <param name="cache">The semantic and native resolution cache.</param>
    /// <param name="binding">The navigation binding shared by resolved public entities.</param>
    /// <param name="layoutRegistry">The registry of automatic-detection layout candidates.</param>
    /// <param name="runtimeStaticFieldStorageResolver">The optional public-runtime-API field-storage resolver.</param>
    /// <param name="callTimeout">The timeout applied to individual remote runtime calls.</param>
    private ResolutionSession(TargetProcess process, Il2CppTarget target, Il2CppRuntime runtime, Il2CppRuntimeCatalog runtimeCatalog, IIl2CppResolutionBackend backend, ResolutionCache cache, ResolutionBinding binding, Il2CppLayoutRegistry layoutRegistry, Il2CppRuntimeStaticFieldStorageResolver runtimeStaticFieldStorageResolver, TimeSpan callTimeout)
    {
        _process = process;
        _target = target;
        _runtime = runtime;
        _runtimeCatalog = runtimeCatalog;
        _backend = backend;
        _cache = cache;
        _binding = binding;
        _layoutRegistry = layoutRegistry;
        _runtimeStaticFieldStorageResolver = runtimeStaticFieldStorageResolver;
        _fieldValueReader = new Il2CppFieldValueReader(target.Memory, runtimeCatalog.GetTypeCatalog());
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Attaches to a Windows x64 process and initializes the complete resolver stack with built-in layout candidates and empty session caches.
    /// </summary>
    /// <param name="processId">The operating system identifier of the target process.</param>
    /// <param name="callTimeout">The maximum duration allowed for each individual remote runtime invocation.</param>
    /// <returns>A fully initialized resolution session owning the target process.</returns>
    public static ResolutionSession Attach(int processId, TimeSpan callTimeout)
    {
        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The IL2CPP runtime call timeout must be positive.");

        TargetProcess process = TargetProcess.Attach(processId);

        try
        {
            Il2CppTarget target = new Il2CppTargetDetector(process).Detect();
            Il2CppRuntimeExports exports = Il2CppRuntimeExports.Resolve(target);
            Il2CppRuntime runtime = new(target, exports);
            Il2CppRuntimeCatalog runtimeCatalog = new(runtime, callTimeout);
            ResolutionCache cache = new();
            ResolutionBinding binding = new();
            RuntimeResolutionBackend backend = new(runtime, runtimeCatalog, cache, binding, callTimeout);
            Il2CppLayoutRegistry layoutRegistry = new(Il2CppClassLayouts.BuiltIn, Il2CppMethodInfoLayouts.BuiltIn);
            Il2CppRuntimeStaticFieldStorageResolver runtimeStaticFieldStorageResolver = new(runtime, target.Memory, callTimeout);
            ResolutionSession session = new(process, target, runtime, runtimeCatalog, backend, cache, binding, layoutRegistry, runtimeStaticFieldStorageResolver, callTimeout);
            binding.Bind(session);
            return session;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>Sets the MethodInfo layout used by subsequent default method-code resolutions.</summary>
    /// <param name="layout">The explicit immutable MethodInfo layout to select.</param>
    public void SetMethodInfoLayout(Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _selectedMethodInfoLayout = layout;
        }
    }

    /// <summary>Sets the Il2CppClass layout used by subsequent default static-field storage resolutions.</summary>
    /// <param name="layout">The explicit immutable class layout to select.</param>
    public void SetFieldStorageLayout(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _selectedFieldStorageLayout = layout;
        }
    }

    /// <summary>Removes the explicitly selected MethodInfo layout and restores automatic selection for subsequent default method-code resolutions.</summary>
    public void ResetMethodInfoLayout()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _selectedMethodInfoLayout = null;
        }
    }

    /// <summary>Removes the explicitly selected field-storage layout and restores automatic runtime-API or detected-layout selection.</summary>
    public void ResetFieldStorageLayout()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _selectedFieldStorageLayout = null;
        }
    }

    /// <summary>Registers an additional Il2CppClass layout candidate for future automatic field-storage layout detection.</summary>
    /// <param name="layout">The immutable class layout to register.</param>
    /// <returns><see langword="true"/> when the candidate was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterFieldStorageLayout(Il2CppClassLayout layout)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            bool added = _layoutRegistry.RegisterClassLayout(layout);

            if (added)
                _detectedFieldStorageLayout = null;

            return added;
        }
    }

    /// <summary>Registers an additional MethodInfo layout candidate for future automatic method-code layout detection.</summary>
    /// <param name="layout">The immutable MethodInfo layout to register.</param>
    /// <returns><see langword="true"/> when the candidate was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterMethodInfoLayout(Il2CppMethodInfoLayout layout)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            bool added = _layoutRegistry.RegisterMethodInfoLayout(layout);

            if (added)
                _detectedMethodInfoLayout = null;

            return added;
        }
    }

    /// <summary>Gets every loaded assembly from the session-scoped runtime snapshot.</summary>
    /// <returns>Every resolved assembly currently registered in the active IL2CPP domain.</returns>
    public IReadOnlyList<ResolvedAssembly> GetAssemblies()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _backend.GetAssemblies();
        }
    }

    /// <summary>Resolves an assembly through the active semantic backend.</summary>
    /// <param name="query">The semantic assembly identity to resolve.</param>
    /// <returns>The resolved IL2CPP assembly.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _backend.ResolveAssembly(query);
        }
    }

    /// <summary>Resolves a managed type through the active semantic backend.</summary>
    /// <param name="query">The semantic type identity to resolve.</param>
    /// <returns>The resolved IL2CPP type.</returns>
    public ResolvedType ResolveType(TypeQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _backend.ResolveType(query);
        }
    }

    /// <summary>Resolves a managed method through the active semantic backend.</summary>
    /// <param name="query">The complete semantic method signature to resolve.</param>
    /// <returns>The resolved IL2CPP method.</returns>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _backend.ResolveMethod(query);
        }
    }

    /// <summary>
    /// Resolves native method code using the globally selected MethodInfo layout when configured, otherwise using a lazily detected session layout.
    /// </summary>
    /// <param name="query">The semantic method query to resolve.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            ResolvedMethod method = _backend.ResolveMethod(query);
            Il2CppMethodInfoLayout layout = _selectedMethodInfoLayout ?? GetDetectedMethodInfoLayout(method.DeclaringType);
            return ResolveMethodCodeCore(method, layout);
        }
    }

    /// <summary>Resolves native method code using one explicit layout override without modifying session configuration.</summary>
    /// <param name="query">The semantic method query to resolve.</param>
    /// <param name="layout">The one-shot MethodInfo layout override.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query, Il2CppMethodInfoLayout layout)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(layout);
            ResolvedMethod method = _backend.ResolveMethod(query);
            return ResolveMethodCodeCore(method, layout);
        }
    }

    /// <summary>Resolves a managed property through the active semantic backend.</summary>
    /// <param name="query">The semantic property identity to resolve.</param>
    /// <returns>The resolved IL2CPP property.</returns>
    public ResolvedProperty ResolveProperty(PropertyQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            return _backend.ResolveProperty(query);
        }
    }

    /// <summary>Resolves a managed field through the active semantic backend.</summary>
    /// <param name="query">The semantic field identity to resolve.</param>
    /// <returns>The resolved IL2CPP field.</returns>
    public ResolvedField ResolveField(FieldQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            return _backend.ResolveField(query);
        }
    }

    /// <summary>
    /// Resolves normal static-field storage using the globally selected class layout when configured; otherwise the public runtime storage API is preferred, followed by a previously auto-detected layout.
    /// </summary>
    /// <param name="query">The semantic field query to resolve.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            ResolvedField field = _backend.ResolveField(query);
            return ResolveFieldStorageDefaultCore(field);
        }
    }

    /// <summary>Resolves normal static-field storage using one explicit class-layout override without modifying session configuration.</summary>
    /// <param name="query">The semantic field query to resolve.</param>
    /// <param name="layout">The one-shot class-layout override.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, Il2CppClassLayout layout)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(layout);
            ResolvedField field = _backend.ResolveField(query);
            return ResolveFieldStorageCore(field, layout);
        }
    }

    /// <summary>
    /// Resolves normal static-field storage after explicitly requesting automatic class-layout detection from multiple field queries.
    /// The detected layout is retained for subsequent default calls until the cache generation is cleared or new class-layout candidates are registered.
    /// </summary>
    /// <param name="query">The semantic target field query.</param>
    /// <param name="layoutEvidenceQueries">Additional normal static fields used as independent structural evidence.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, IReadOnlyList<FieldQuery> layoutEvidenceQueries)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(layoutEvidenceQueries);
            ResolvedField field = _backend.ResolveField(query);
            Il2CppClassLayout layout = DetectFieldStorageLayout(field, layoutEvidenceQueries);
            return ResolveFieldStorageCore(field, layout);
        }
    }

    /// <summary>
    /// Invalidates semantic results, runtime snapshots and automatically detected layouts while preserving explicit consumer layout selections and registered candidates.
    /// </summary>
    public void ClearCache()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _cache.Clear();
            _runtimeCatalog.Clear();
            _detectedMethodInfoLayout = null;
            _detectedFieldStorageLayout = null;
            _binding.AdvanceGeneration();
        }
    }

    /// <summary>Enumerates every type exposed by a resolved assembly after validating that the originating entity belongs to the active cache generation.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>Every resolved type exposed by the assembly image.</returns>
    IReadOnlyList<ResolvedType> IResolutionNavigator.GetTypes(ResolvedAssembly assembly, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(assembly);
            return _backend.GetTypes(assembly);
        }
    }

    /// <summary>Resolves one type relative to a resolved assembly after validating the originating cache generation.</summary>
    /// <param name="assembly">The resolved assembly containing the requested type.</param>
    /// <param name="namespaceName">The exact managed namespace. An empty namespace is valid.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>The resolved runtime type.</returns>
    ResolvedType IResolutionNavigator.ResolveType(ResolvedAssembly assembly, string namespaceName, string typeName, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(assembly);
            ArgumentNullException.ThrowIfNull(namespaceName);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            return _backend.ResolveType(new TypeQuery(assembly.Name, namespaceName, typeName));
        }
    }

    /// <summary>Enumerates every method declared by a resolved type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>Every declared resolved method with complete semantic signatures.</returns>
    IReadOnlyList<ResolvedMethod> IResolutionNavigator.GetMethods(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            return _backend.GetMethods(type);
        }
    }

    /// <summary>Enumerates every overload with an exact method name after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>Every resolved overload sharing the requested name.</returns>
    IReadOnlyList<ResolvedMethod> IResolutionNavigator.GetMethods(ResolvedType type, string name, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _backend.GetMethods(type, name);
        }
    }

    /// <summary>Resolves one exact method overload relative to a resolved declaring type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the overload.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    ResolvedMethod IResolutionNavigator.ResolveMethod(ResolvedType type, string methodName, IReadOnlyList<string> parameterTypeNames, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(parameterTypeNames);
            return _backend.ResolveMethod(new MethodQuery(type.Query, methodName, parameterTypeNames.ToArray()));
        }
    }

    /// <summary>Enumerates every property declared by a resolved type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>Every declared resolved property.</returns>
    IReadOnlyList<ResolvedProperty> IResolutionNavigator.GetProperties(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            return _backend.GetProperties(type);
        }
    }

    /// <summary>Enumerates every property with an exact property name after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed property name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>Every resolved property sharing the requested name.</returns>
    IReadOnlyList<ResolvedProperty> IResolutionNavigator.GetProperties(ResolvedType type, string name, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _backend.GetProperties(type, name);
        }
    }

    /// <summary>Resolves one exact property relative to a resolved declaring type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="propertyName">The exact managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying the property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved property matching the requested signature.</returns>
    ResolvedProperty IResolutionNavigator.ResolveProperty(ResolvedType type, string propertyName, IReadOnlyList<string> indexParameterTypeNames, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            ArgumentNullException.ThrowIfNull(indexParameterTypeNames);
            return _backend.ResolveProperty(new PropertyQuery(type.Query, propertyName, indexParameterTypeNames.ToArray()));
        }
    }

    /// <summary>Enumerates every field declared by a resolved type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>Every declared resolved field with semantic type and storage metadata.</returns>
    IReadOnlyList<ResolvedField> IResolutionNavigator.GetFields(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            return _backend.GetFields(type);
        }
    }

    /// <summary>Resolves one exact field relative to a resolved declaring type after validating the originating cache generation.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    ResolvedField IResolutionNavigator.ResolveField(ResolvedType type, string fieldName, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            return _backend.ResolveField(new FieldQuery(type.Query, fieldName));
        }
    }

    /// <summary>Gets the parent type of a resolved runtime type after validating the originating cache generation.</summary>
    ResolvedType? IResolutionNavigator.GetBaseType(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            nint parentAddress = _runtimeCatalog.GetTypeCatalog().GetParent(type.ClassAddress);
            return parentAddress == 0 ? null : _backend.ResolveTypeByClassAddress(parentAddress);
        }
    }

    /// <summary>Gets every interface associated with a resolved runtime type after validating the originating cache generation.</summary>
    IReadOnlyList<ResolvedType> IResolutionNavigator.GetInterfaces(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            IReadOnlyList<nint> addresses = _runtimeCatalog.GetTypeCatalog().GetInterfaces(type.ClassAddress);
            List<ResolvedType> results = new(addresses.Count);

            foreach (nint address in addresses)
                results.Add(_backend.ResolveTypeByClassAddress(address));

            return results.AsReadOnly();
        }
    }

    /// <summary>Gets every nested type declared by a resolved runtime type after validating the originating cache generation.</summary>
    IReadOnlyList<ResolvedType> IResolutionNavigator.GetNestedTypes(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            IReadOnlyList<nint> addresses = _runtimeCatalog.GetTypeCatalog().GetNestedTypes(type.ClassAddress);
            List<ResolvedType> results = new(addresses.Count);

            foreach (nint address in addresses)
                results.Add(_backend.ResolveTypeByClassAddress(address));

            return results.AsReadOnly();
        }
    }

    /// <summary>Gets the declaring type of a nested resolved runtime type after validating the originating cache generation.</summary>
    ResolvedType? IResolutionNavigator.GetDeclaringType(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);
            nint declaringAddress = _runtimeCatalog.GetTypeCatalog().GetDeclaringType(type.ClassAddress);
            return declaringAddress == 0 ? null : _backend.ResolveTypeByClassAddress(declaringAddress);
        }
    }

    /// <summary>Gets cached public type metadata after validating the originating cache generation.</summary>
    ResolvedTypeMetadata IResolutionNavigator.GetTypeMetadata(ResolvedType type, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(type);

            if (_cache.TryGetTypeMetadata(type.ClassAddress, out ResolvedTypeMetadata? cachedMetadata))
                return cachedMetadata;

            RuntimeClassMetadata metadata = _runtimeCatalog.GetTypeCatalog().GetClassMetadata(type.ClassAddress);
            ResolvedTypeMetadata result = new(metadata.Attributes, metadata.MetadataToken, metadata.IsValueType, metadata.IsEnum, metadata.IsBlittable, metadata.IsGeneric, metadata.IsInflated, metadata.ValueSize, metadata.ValueAlignment);
            _cache.StoreTypeMetadata(type.ClassAddress, result);
            return result;
        }
    }

    /// <summary>Gets cached public method metadata after validating the originating cache generation.</summary>
    ResolvedMethodMetadata IResolutionNavigator.GetMethodMetadata(ResolvedMethod method, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(method);

            if (_cache.TryGetMethodMetadata(method.MethodInfoAddress, out ResolvedMethodMetadata? cachedMetadata))
                return cachedMetadata;

            RuntimeMethodMetadata metadata = _runtimeCatalog.GetMethodMetadataCatalog().GetMetadata(method.MethodInfoAddress);
            ResolvedMethodMetadata result = new(metadata.Attributes, metadata.ImplementationAttributes, metadata.MetadataToken, metadata.IsGeneric, metadata.IsInflated);
            _cache.StoreMethodMetadata(method.MethodInfoAddress, result);
            return result;
        }
    }

    /// <summary>Maps a resolved method to native code through the session's active MethodInfo layout policy after validating the originating cache generation.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode IResolutionNavigator.ResolveMethodCode(ResolvedMethod method, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(method);
            Il2CppMethodInfoLayout layout = _selectedMethodInfoLayout ?? GetDetectedMethodInfoLayout(method.DeclaringType);
            return ResolveMethodCodeCore(method, layout);
        }
    }

    /// <summary>Maps a resolved method to native code through one explicit MethodInfo layout override after validating the originating cache generation.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode IResolutionNavigator.ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(layout);
            return ResolveMethodCodeCore(method, layout);
        }
    }

    /// <summary>Maps a resolved normal static field through the session's active storage-selection policy after validating the originating cache generation.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage IResolutionNavigator.ResolveFieldStorage(ResolvedField field, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            return ResolveFieldStorageDefaultCore(field);
        }
    }

    /// <summary>Maps a resolved normal static field through one explicit Il2CppClass layout override after validating the originating cache generation.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage IResolutionNavigator.ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(layout);
            return ResolveFieldStorageCore(field, layout);
        }
    }

    /// <summary>Reads a supported scalar from a normal static field through the session's active field-storage policy after validating the originating cache generation.</summary>
    T IResolutionNavigator.ReadStaticField<T>(ResolvedField field, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ResolvedFieldStorage storage = ResolveFieldStorageDefaultCore(field);
            return _fieldValueReader.ReadStatic<T>(field, storage);
        }
    }

    /// <summary>Reads a supported scalar from a normal static field through one explicit Il2CppClass layout override after validating the originating cache generation.</summary>
    T IResolutionNavigator.ReadStaticField<T>(ResolvedField field, Il2CppClassLayout layout, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(layout);
            ResolvedFieldStorage storage = ResolveFieldStorageCore(field, layout);
            return _fieldValueReader.ReadStatic<T>(field, storage);
        }
    }

    /// <summary>Reads a managed reference from a normal static field through the session's active field-storage policy after validating the originating cache generation.</summary>
    nint IResolutionNavigator.ReadStaticFieldReference(ResolvedField field, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ResolvedFieldStorage storage = ResolveFieldStorageDefaultCore(field);
            return _fieldValueReader.ReadStaticReference(field, storage);
        }
    }

    /// <summary>Reads a managed reference from a normal static field through one explicit Il2CppClass layout override after validating the originating cache generation.</summary>
    nint IResolutionNavigator.ReadStaticFieldReference(ResolvedField field, Il2CppClassLayout layout, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(layout);
            ResolvedFieldStorage storage = ResolveFieldStorageCore(field, layout);
            return _fieldValueReader.ReadStaticReference(field, storage);
        }
    }

    /// <summary>Reads an enum from a normal static field through the session's active field-storage policy after validating the originating cache generation.</summary>
    TEnum IResolutionNavigator.ReadStaticFieldEnum<TEnum>(ResolvedField field, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ResolvedFieldStorage storage = ResolveFieldStorageDefaultCore(field);
            return _fieldValueReader.ReadStaticEnum<TEnum>(field, storage);
        }
    }

    /// <summary>Reads an enum from a normal static field through one explicit Il2CppClass layout override after validating the originating cache generation.</summary>
    TEnum IResolutionNavigator.ReadStaticFieldEnum<TEnum>(ResolvedField field, Il2CppClassLayout layout, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(layout);
            ResolvedFieldStorage storage = ResolveFieldStorageCore(field, layout);
            return _fieldValueReader.ReadStaticEnum<TEnum>(field, storage);
        }
    }

    /// <summary>Reads a supported scalar from an instance field relative to the specified remote IL2CPP object after validating the originating cache generation.</summary>
    T IResolutionNavigator.ReadInstanceField<T>(ResolvedField field, nint instanceAddress, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            return _fieldValueReader.ReadInstance<T>(field, instanceAddress);
        }
    }

    /// <summary>Reads a managed reference from an instance field relative to the specified remote IL2CPP object after validating the originating cache generation.</summary>
    nint IResolutionNavigator.ReadInstanceFieldReference(ResolvedField field, nint instanceAddress, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            return _fieldValueReader.ReadInstanceReference(field, instanceAddress);
        }
    }

    /// <summary>Reads an enum from an instance field relative to the specified remote IL2CPP object after validating the originating cache generation.</summary>
    TEnum IResolutionNavigator.ReadInstanceFieldEnum<TEnum>(ResolvedField field, nint instanceAddress, long generation)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ValidateGeneration(generation);
            ArgumentNullException.ThrowIfNull(field);
            return _fieldValueReader.ReadInstanceEnum<TEnum>(field, instanceAddress);
        }
    }

    /// <summary>Releases the complete target-specific session and its owned native process handle.</summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _cache.Clear();
            _runtimeCatalog.Clear();
            _process.Dispose();
            _disposed = true;
        }
    }

    /// <summary>Maps an already resolved method to native code using the specified layout and layout-sensitive cache key.</summary>
    /// <param name="method">The resolved semantic method.</param>
    /// <param name="layout">The exact MethodInfo layout to apply.</param>
    /// <returns>The validated native method-code mapping.</returns>
    private ResolvedMethodCode ResolveMethodCodeCore(ResolvedMethod method, Il2CppMethodInfoLayout layout)
    {
        if (_cache.TryGetMethodCode(method.MethodInfoAddress, layout, out ResolvedMethodCode? cachedCode))
            return cachedCode;

        Il2CppMethodPointerResolver pointerResolver = new(_target, layout);
        ResolvedMethodCode result = pointerResolver.Resolve(method);
        _cache.StoreMethodCode(result, layout);
        return result;
    }

    /// <summary>Maps an already resolved field using the session's active field-storage selection policy.</summary>
    /// <param name="field">The resolved normal static field.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    private ResolvedFieldStorage ResolveFieldStorageDefaultCore(ResolvedField field)
    {
        if (_selectedFieldStorageLayout is not null)
            return ResolveFieldStorageCore(field, _selectedFieldStorageLayout);

        if (_runtime.Capabilities.HasStaticFieldStorageApi)
            return ResolveRuntimeFieldStorageCore(field);

        if (_detectedFieldStorageLayout is not null)
            return ResolveFieldStorageCore(field, _detectedFieldStorageLayout);

        throw new InvalidOperationException("No field-storage layout is selected and the target does not expose the public IL2CPP static-field storage API. Call SetFieldStorageLayout or provide layout evidence for automatic detection.");
    }

    /// <summary>Maps an already resolved static field through the specified structural class layout.</summary>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="layout">The exact Il2CppClass layout to apply.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    private ResolvedFieldStorage ResolveFieldStorageCore(ResolvedField field, Il2CppClassLayout layout)
    {
        if (_cache.TryGetFieldStorage(field, layout, out ResolvedFieldStorage? cachedStorage))
            return cachedStorage;

        Il2CppStaticFieldStorageResolver resolver = new(_target, layout);
        ResolvedFieldStorage storage = resolver.Resolve(field);
        _cache.StoreFieldStorage(storage, layout);
        return storage;
    }

    /// <summary>Maps an already resolved static field through the optional public IL2CPP runtime storage API.</summary>
    /// <param name="field">The resolved normal static field.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    private ResolvedFieldStorage ResolveRuntimeFieldStorageCore(ResolvedField field)
    {
        if (_cache.TryGetRuntimeFieldStorage(field, out ResolvedFieldStorage? cachedStorage))
            return cachedStorage;

        ResolvedFieldStorage storage = _runtimeStaticFieldStorageResolver.Resolve(field);
        _cache.StoreRuntimeFieldStorage(storage);
        return storage;
    }

    /// <summary>Gets or lazily detects the target-wide MethodInfo layout from a method sample on the supplied declaring type.</summary>
    /// <param name="evidenceType">The resolved type whose methods provide structural evidence.</param>
    /// <returns>The detected MethodInfo layout.</returns>
    private Il2CppMethodInfoLayout GetDetectedMethodInfoLayout(ResolvedType evidenceType)
    {
        if (_detectedMethodInfoLayout is not null)
            return _detectedMethodInfoLayout;

        Il2CppClassMemberCatalog members = _runtimeCatalog.GetClassMembers(evidenceType.ClassAddress);
        IReadOnlyList<nint> availableMethods = members.GetMethodAddresses();

        if (availableMethods.Count < MinimumValidatedMethodCount)
            throw new InvalidDataException($"Type '{evidenceType.Query.Namespace}.{evidenceType.Query.Name}' exposes only {availableMethods.Count} method(s), which is insufficient to validate the IL2CPP MethodInfo layout. Configure an explicit layout with SetMethodInfoLayout to bypass automatic detection.");

        int sampleCount = Math.Min(availableMethods.Count, MethodInfoLayoutSampleSize);
        nint[] sample = availableMethods.Take(sampleCount).ToArray();
        IReadOnlyList<Il2CppMethodInfoLayout> candidates = _layoutRegistry.GetMethodInfoLayouts();
        Il2CppMethodInfoLayoutDetector detector = new(_target, candidates, MinimumValidatedMethodCount);
        Il2CppMethodInfoLayoutDetectionResult detection = detector.Detect(sample);
        _detectedMethodInfoLayout = detection.Layout;
        return _detectedMethodInfoLayout;
    }

    /// <summary>Detects and stores the target-wide Il2CppClass layout from normal static fields belonging to multiple independent classes.</summary>
    /// <param name="targetField">The target field that also contributes evidence when it is a normal static field.</param>
    /// <param name="evidenceQueries">Additional normal static-field queries used as structural evidence.</param>
    /// <returns>The unique detected Il2CppClass layout.</returns>
    private Il2CppClassLayout DetectFieldStorageLayout(ResolvedField targetField, IReadOnlyList<FieldQuery> evidenceQueries)
    {
        List<ResolvedField> evidence = new(checked(evidenceQueries.Count + 1));
        HashSet<nint> fieldAddresses = new();

        if (targetField.StorageKind == FieldStorageKind.Static)
        {
            evidence.Add(targetField);
            fieldAddresses.Add(targetField.FieldInfoAddress);
        }

        foreach (FieldQuery query in evidenceQueries)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResolvedField field = _backend.ResolveField(query);

            if (field.StorageKind != FieldStorageKind.Static)
                throw new InvalidDataException($"Layout evidence field '{field.Query.Name}' uses storage kind '{field.StorageKind}' instead of normal static storage.");

            if (fieldAddresses.Add(field.FieldInfoAddress))
                evidence.Add(field);
        }

        IReadOnlyList<Il2CppClassLayout> candidates = _layoutRegistry.GetClassLayouts();
        Il2CppClassLayoutDetector detector = new(_target, candidates, MinimumValidatedClassCount);
        Il2CppClassLayoutDetectionResult detection = detector.Detect(evidence);
        _detectedFieldStorageLayout = detection.Layout;
        return _detectedFieldStorageLayout;
    }

    /// <summary>Rejects navigation through a resolved entity created before the most recent cache invalidation.</summary>
    /// <param name="generation">The cache generation stamped onto the resolved entity.</param>
    /// <exception cref="InvalidOperationException">Thrown when the entity belongs to an invalidated cache generation.</exception>
    private void ValidateGeneration(long generation)
    {
        if (generation != _binding.Generation)
            throw new InvalidOperationException("The resolved entity belongs to an invalidated resolver generation. Resolve the entity again before navigating from it.");
    }

    /// <summary>Throws when an operation is attempted after the session has released its target process.</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
