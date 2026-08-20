using UnityIl2CppResolver.Il2Cpp.Runtime.Model;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Maintains the session-scoped local snapshot of expensive IL2CPP runtime discovery data.
/// The catalogue resolves the domain and assembly list once, reuses class-member snapshots by <c>Il2CppClass*</c>, and can be explicitly invalidated together with semantic resolution caches.
/// </summary>
internal sealed class Il2CppRuntimeCatalog
{
    /// <summary>
    /// Provides the live runtime operations used when a requested catalogue entry has not yet been materialized.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Defines the timeout applied to each individual remote runtime call.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Stores the active domain pointer after its first successful lookup.
    /// </summary>
    private nint? _domainAddress;

    /// <summary>
    /// Stores the normalized assembly snapshot after its first successful enumeration.
    /// </summary>
    private Dictionary<string, Il2CppAssemblyInfo>? _assemblies;

    /// <summary>
    /// Stores lazy class-member snapshots indexed by native <c>Il2CppClass*</c> identity.
    /// </summary>
    private readonly Dictionary<nint, Il2CppClassMemberCatalog> _classMembers = new();

    /// <summary>
    /// Initializes a session-scoped runtime catalogue.
    /// </summary>
    /// <param name="runtime">The live IL2CPP runtime used to materialize missing catalogue entries.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppRuntimeCatalog(Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Resolves an assembly from the local runtime snapshot, materializing the complete assembly catalogue only on the first request.
    /// </summary>
    /// <param name="name">The simple assembly name or runtime image name to locate.</param>
    /// <returns>The unique assembly description matching the normalized name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the assembly is absent from the active runtime domain.</exception>
    /// <exception cref="InvalidDataException">Thrown when the runtime exposes multiple assemblies with the same normalized identity.</exception>
    public Il2CppAssemblyInfo ResolveAssembly(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureAssemblies();
        string normalized = NormalizeAssemblyName(name);

        if (!_assemblies!.TryGetValue(normalized, out Il2CppAssemblyInfo? assembly))
            throw new KeyNotFoundException($"IL2CPP assembly '{name}' was not found in the active runtime domain.");

        return assembly;
    }

    /// <summary>
    /// Gets the lazy member catalogue associated with one runtime class.
    /// </summary>
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

    /// <summary>
    /// Invalidates every locally materialized runtime snapshot while preserving the live runtime attachment itself.
    /// </summary>
    public void Clear()
    {
        _domainAddress = null;
        _assemblies = null;
        _classMembers.Clear();
        _runtime.ClearCaches();
    }

    /// <summary>
    /// Materializes the active domain and complete assembly snapshot once for the current catalogue generation.
    /// </summary>
    private void EnsureAssemblies()
    {
        if (_assemblies is not null)
            return;

        if (_domainAddress is null)
            _domainAddress = _runtime.GetDomain(_callTimeout);

        nint domain = _domainAddress.Value;
        IReadOnlyList<Il2CppAssemblyInfo> assemblies = _runtime.GetAssemblyInfos(domain, _callTimeout);
        Dictionary<string, Il2CppAssemblyInfo> index = new(StringComparer.OrdinalIgnoreCase);

        foreach (Il2CppAssemblyInfo assembly in assemblies)
        {
            string normalized = NormalizeAssemblyName(assembly.Name);

            if (!index.TryAdd(normalized, assembly))
                throw new InvalidDataException($"Multiple IL2CPP assemblies normalize to semantic identity '{normalized}'.");
        }

        _assemblies = index;
    }

    /// <summary>
    /// Normalizes an assembly identifier by removing conventional managed image suffixes.
    /// </summary>
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
