using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;
using UnityIl2CppResolver.Il2Cpp.Runtime;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Resolves semantic IL2CPP entities by interrogating the public native runtime API exposed by the target process.
/// This backend converts assembly and type queries into live <c>Il2CppAssembly</c>, <c>Il2CppImage</c> and <c>Il2CppClass</c> entities without relying on external metadata files, hardcoded RVAs or native code signatures.
/// It currently performs uncached runtime resolution; session-level caching can be introduced later without changing the semantic query or result models.
/// </summary>
internal sealed class RuntimeResolutionBackend
{
    /// <summary>
    /// Represents the low-level IL2CPP runtime abstraction used to enumerate assemblies and resolve classes.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Defines the maximum duration allowed for each individual remote IL2CPP API invocation performed by this backend.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Initializes semantic resolution over the specified IL2CPP runtime.
    /// </summary>
    /// <param name="runtime">The low-level IL2CPP runtime abstraction associated with the target process.</param>
    /// <param name="callTimeout">The finite timeout applied independently to each native runtime call.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="runtime"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="callTimeout"/> is zero or negative.
    /// </exception>
    public RuntimeResolutionBackend(Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
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

        return new ResolvedAssembly(query, match.Name, match.AssemblyAddress, match.ImageAddress);
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

        ResolvedAssembly assembly = ResolveAssembly(query.Assembly);
        nint classAddress = _runtime.GetClass(assembly.ImageAddress, query.Namespace, query.Name, _callTimeout);

        if (classAddress == 0)
            throw new KeyNotFoundException($"IL2CPP type '{query.Namespace}.{query.Name}' was not found in assembly '{assembly.Name}'.");

        return new ResolvedType(query, assembly, classAddress);
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
}