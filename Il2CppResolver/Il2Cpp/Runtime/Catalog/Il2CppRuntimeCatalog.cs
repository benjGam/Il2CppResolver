using RuntimeAssemblyInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppAssemblyInfo;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Maintains the session-scoped local snapshot of expensive IL2CPP runtime discovery data.
/// The catalogue resolves the domain and assembly list once, reuses image-type, class-member, type-introspection and method-metadata snapshots by native runtime identity, and can be explicitly invalidated together with semantic resolution caches.
/// </summary>
internal sealed class Il2CppRuntimeCatalog
{
    /// <summary>Provides the live runtime operations used when a requested catalogue entry has not yet been materialized.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Defines the timeout applied to each individual remote runtime call.</summary>
    private readonly TimeSpan _callTimeout;
    /// <summary>Stores the active domain pointer after its first successful lookup.</summary>
    private nint? _domainAddress;
    /// <summary>Stores the immutable assembly snapshot after its first successful enumeration.</summary>
    private IReadOnlyList<RuntimeAssemblyInfo>? _assemblySnapshot;
    /// <summary>Stores assemblies indexed by normalized semantic identity after the first snapshot is materialized.</summary>
    private Dictionary<string, RuntimeAssemblyInfo>? _assemblies;
    /// <summary>Stores assemblies indexed by native image identity for cross-assembly relationship navigation.</summary>
    private Dictionary<nint, RuntimeAssemblyInfo>? _assembliesByImageAddress;
    /// <summary>Stores lazy image-type snapshots indexed by native <c>Il2CppImage*</c> identity.</summary>
    private readonly Dictionary<nint, Il2CppImageTypeCatalog> _imageTypes = new();
    /// <summary>Stores lazy class-member snapshots indexed by native <c>Il2CppClass*</c> identity.</summary>
    private readonly Dictionary<nint, Il2CppClassMemberCatalog> _classMembers = new();
    /// <summary>Stores field type descriptors, class metadata and relationship snapshots.</summary>
    private readonly Il2CppTypeCatalog _types;
    /// <summary>Stores lazily inspected method metadata.</summary>
    private readonly Il2CppMethodMetadataCatalog _methodMetadata;

    /// <summary>Initializes a session-scoped runtime catalogue.</summary>
    /// <param name="runtime">The live IL2CPP runtime used to materialize missing catalogue entries.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppRuntimeCatalog(Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _callTimeout = callTimeout;
        _types = new Il2CppTypeCatalog(runtime, callTimeout);
        _methodMetadata = new Il2CppMethodMetadataCatalog(runtime, callTimeout);
    }

    /// <summary>Gets the active IL2CPP domain pointer for the current cache generation, resolving it only once.</summary>
    /// <returns>The non-null active <c>Il2CppDomain*</c> address.</returns>
    public nint GetDomainAddress()
    {
        if (_domainAddress is null)
            _domainAddress = _runtime.GetDomain(_callTimeout);

        return _domainAddress.Value;
    }

    /// <summary>Gets the immutable assembly snapshot for the current cache generation, materializing it only on the first request.</summary>
    /// <returns>Every assembly currently registered in the active IL2CPP domain.</returns>
    public IReadOnlyList<RuntimeAssemblyInfo> GetAssemblies()
    {
        EnsureAssemblies();
        return _assemblySnapshot!;
    }

    /// <summary>Resolves an assembly from the local runtime snapshot by normalized semantic name.</summary>
    /// <param name="name">The simple assembly name or runtime image name to locate.</param>
    /// <returns>The unique assembly description matching the normalized name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the assembly is absent from the active runtime domain.</exception>
    public RuntimeAssemblyInfo ResolveAssembly(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureAssemblies();
        string normalized = NormalizeAssemblyName(name);

        if (!_assemblies!.TryGetValue(normalized, out RuntimeAssemblyInfo? assembly))
            throw new KeyNotFoundException($"IL2CPP assembly '{name}' was not found in the active runtime domain.");

        return assembly;
    }

    /// <summary>Resolves an assembly from the local runtime snapshot by native image identity.</summary>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> address to locate.</param>
    /// <returns>The unique assembly owning the image.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no assembly snapshot entry owns the specified image.</exception>
    public RuntimeAssemblyInfo ResolveAssemblyByImageAddress(nint imageAddress)
    {
        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image address cannot be zero.");

        EnsureAssemblies();

        if (!_assembliesByImageAddress!.TryGetValue(imageAddress, out RuntimeAssemblyInfo? assembly))
            throw new KeyNotFoundException($"No loaded IL2CPP assembly owns image 0x{imageAddress:X}.");

        return assembly;
    }

    /// <summary>Gets the lazy type catalogue associated with one runtime image.</summary>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> whose types should be cached.</param>
    /// <returns>The existing or newly created image-type catalogue.</returns>
    public Il2CppImageTypeCatalog GetImageTypes(nint imageAddress)
    {
        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image address cannot be zero.");

        if (_imageTypes.TryGetValue(imageAddress, out Il2CppImageTypeCatalog? types))
            return types;

        types = new Il2CppImageTypeCatalog(_runtime, imageAddress, _callTimeout);
        _imageTypes.Add(imageAddress, types);
        return types;
    }

    /// <summary>Gets the lazy member catalogue associated with one runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose members should be cached.</param>
    /// <returns>The existing or newly created class-member catalogue.</returns>
    public Il2CppClassMemberCatalog GetClassMembers(nint classAddress)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class address cannot be zero.");

        if (_classMembers.TryGetValue(classAddress, out Il2CppClassMemberCatalog? members))
            return members;

        members = new Il2CppClassMemberCatalog(_runtime, classAddress, _callTimeout);
        _classMembers.Add(classAddress, members);
        return members;
    }

    /// <summary>Gets the session-scoped type introspection and relationship catalogue.</summary>
    /// <returns>The type catalogue owned by this runtime snapshot.</returns>
    public Il2CppTypeCatalog GetTypeCatalog()
    {
        return _types;
    }

    /// <summary>Gets the session-scoped method metadata catalogue.</summary>
    /// <returns>The method metadata catalogue owned by this runtime snapshot.</returns>
    public Il2CppMethodMetadataCatalog GetMethodMetadataCatalog()
    {
        return _methodMetadata;
    }

    /// <summary>Invalidates every locally materialized runtime snapshot while preserving the live runtime attachment itself.</summary>
    public void Clear()
    {
        _domainAddress = null;
        _assemblySnapshot = null;
        _assemblies = null;
        _assembliesByImageAddress = null;
        _imageTypes.Clear();
        _classMembers.Clear();
        _types.Clear();
        _methodMetadata.Clear();
        _runtime.ClearCaches();
    }

    /// <summary>Materializes the active domain and complete assembly snapshot once for the current catalogue generation.</summary>
    private void EnsureAssemblies()
    {
        if (_assemblies is not null && _assemblySnapshot is not null && _assembliesByImageAddress is not null)
            return;

        nint domainAddress = GetDomainAddress();
        IReadOnlyList<RuntimeAssemblyInfo> assemblies = _runtime.GetAssemblyInfos(domainAddress, _callTimeout);
        Dictionary<string, RuntimeAssemblyInfo> semanticIndex = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<nint, RuntimeAssemblyInfo> imageIndex = new();

        foreach (RuntimeAssemblyInfo assembly in assemblies)
        {
            string normalized = NormalizeAssemblyName(assembly.Name);

            if (!semanticIndex.TryAdd(normalized, assembly))
                throw new InvalidDataException($"Multiple IL2CPP assemblies normalize to semantic identity '{normalized}'.");

            if (!imageIndex.TryAdd(assembly.ImageAddress, assembly))
                throw new InvalidDataException($"Multiple IL2CPP assemblies reference image 0x{assembly.ImageAddress:X}.");
        }

        _assemblySnapshot = assemblies;
        _assemblies = semanticIndex;
        _assembliesByImageAddress = imageIndex;
    }

    /// <summary>Normalizes an assembly identifier by removing conventional managed image suffixes.</summary>
    /// <param name="name">The simple assembly name or runtime image name to normalize.</param>
    /// <returns>The normalized assembly identity.</returns>
    private static string NormalizeAssemblyName(string name)
    {
        string normalized = name.Trim();

        if (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return normalized[..^4];

        return normalized;
    }
}
