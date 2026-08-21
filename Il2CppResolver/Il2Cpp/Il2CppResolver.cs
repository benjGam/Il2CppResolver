using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp;

/// <summary>
/// Provides the public semantic entry point for resolving and navigating IL2CPP assemblies, types, methods, fields, native method code and normal static-field storage inside a Windows x64 Unity process.
/// The resolver owns one serialized session and exposes a linear configuration API while resolved entities provide session-bound navigation endpoints that reuse the same runtime catalogues and caches.
/// </summary>
public sealed class Il2CppResolver : IDisposable
{
    /// <summary>Defines the default maximum duration allowed for one individual remote IL2CPP runtime invocation.</summary>
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Owns the target-specific resolution session used by every public operation.</summary>
    private readonly ResolutionSession _session;

    /// <summary>Indicates whether this public resolver has already released its owned session.</summary>
    private bool _disposed;

    /// <summary>Gets the operating system identifier of the target process attached to this resolver.</summary>
    public int ProcessId => _session.ProcessId;

    /// <summary>Gets the MethodInfo layout explicitly selected for default method-code resolution, or <see langword="null"/> when automatic selection is active.</summary>
    public Il2CppMethodInfoLayout? MethodInfoLayout => _session.SelectedMethodInfoLayout;

    /// <summary>Gets the Il2CppClass layout explicitly selected for default static-field storage resolution, or <see langword="null"/> when automatic selection is active.</summary>
    public Il2CppClassLayout? FieldStorageLayout => _session.SelectedFieldStorageLayout;

    /// <summary>Initializes the public facade around an owned internal resolution session.</summary>
    /// <param name="session">The target-specific session owned by this resolver instance.</param>
    private Il2CppResolver(ResolutionSession session)
    {
        _session = session;
    }

    /// <summary>Attaches to a Windows x64 Unity IL2CPP process using the default runtime call timeout.</summary>
    /// <param name="processId">The operating system identifier of the target Unity process.</param>
    /// <returns>A fully initialized resolver owning the target attachment.</returns>
    public static Il2CppResolver Attach(int processId)
    {
        return Attach(processId, DefaultCallTimeout);
    }

    /// <summary>Attaches to a Windows x64 Unity IL2CPP process using the specified timeout for individual runtime calls.</summary>
    /// <param name="processId">The operating system identifier of the target Unity process.</param>
    /// <param name="callTimeout">The maximum duration allowed for each individual remote IL2CPP runtime invocation.</param>
    /// <returns>A fully initialized resolver owning the target attachment.</returns>
    public static Il2CppResolver Attach(int processId, TimeSpan callTimeout)
    {
        ResolutionSession session = ResolutionSession.Attach(processId, callTimeout);
        return new Il2CppResolver(session);
    }

    /// <summary>Sets the MethodInfo structural layout used by subsequent default <see cref="ResolveMethodCode(MethodQuery)"/> calls.</summary>
    /// <param name="layout">The immutable MethodInfo layout to select globally for this resolver session.</param>
    public void SetMethodInfoLayout(Il2CppMethodInfoLayout layout)
    {
        ThrowIfDisposed();
        _session.SetMethodInfoLayout(layout);
    }

    /// <summary>Sets the Il2CppClass structural layout used by subsequent default <see cref="ResolveFieldStorage(FieldQuery)"/> calls.</summary>
    /// <param name="layout">The immutable Il2CppClass layout to select globally for this resolver session.</param>
    public void SetFieldStorageLayout(Il2CppClassLayout layout)
    {
        ThrowIfDisposed();
        _session.SetFieldStorageLayout(layout);
    }

    /// <summary>Removes the globally selected MethodInfo layout and restores automatic selection for subsequent default method-code resolutions.</summary>
    public void ResetMethodInfoLayout()
    {
        ThrowIfDisposed();
        _session.ResetMethodInfoLayout();
    }

    /// <summary>Removes the globally selected field-storage layout and restores runtime-API or automatic layout selection.</summary>
    public void ResetFieldStorageLayout()
    {
        ThrowIfDisposed();
        _session.ResetFieldStorageLayout();
    }

    /// <summary>Registers an additional MethodInfo layout candidate for future automatic detection without selecting it globally.</summary>
    /// <param name="layout">The immutable MethodInfo layout candidate to register.</param>
    /// <returns><see langword="true"/> when the candidate was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterMethodInfoLayout(Il2CppMethodInfoLayout layout)
    {
        ThrowIfDisposed();
        return _session.RegisterMethodInfoLayout(layout);
    }

    /// <summary>Registers an additional Il2CppClass layout candidate for future automatic field-storage detection without selecting it globally.</summary>
    /// <param name="layout">The immutable class-layout candidate to register.</param>
    /// <returns><see langword="true"/> when the candidate was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterFieldStorageLayout(Il2CppClassLayout layout)
    {
        ThrowIfDisposed();
        return _session.RegisterFieldStorageLayout(layout);
    }

    /// <summary>
    /// Gets every assembly currently registered in the active IL2CPP domain.
    /// The complete assembly snapshot is materialized only once per cache generation and subsequent calls reuse local session data.
    /// </summary>
    /// <returns>Every resolved assembly currently loaded by the target runtime.</returns>
    public IReadOnlyList<ResolvedAssembly> GetAssemblies()
    {
        ThrowIfDisposed();
        return _session.GetAssemblies();
    }

    /// <summary>Resolves a loaded IL2CPP assembly from its semantic identity.</summary>
    /// <param name="query">The semantic assembly query to resolve.</param>
    /// <returns>The resolved assembly and runtime identity.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveAssembly(query);
    }

    /// <summary>Resolves a managed type from its assembly, namespace and type name.</summary>
    /// <param name="query">The semantic type query to resolve.</param>
    /// <returns>The resolved runtime type and containing assembly.</returns>
    public ResolvedType ResolveType(TypeQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveType(query);
    }

    /// <summary>Resolves a managed method from its declaring type, name and ordered parameter type names.</summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The resolved runtime method and verified semantic signature.</returns>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveMethod(query);
    }

    /// <summary>Resolves native method code using the globally selected MethodInfo layout or automatic layout detection when no layout is selected.</summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The resolved method together with its validated direct native code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveMethodCode(query);
    }

    /// <summary>Resolves native method code using one explicit MethodInfo layout without changing the globally selected session layout.</summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <param name="layout">The one-shot MethodInfo layout override.</param>
    /// <returns>The resolved method together with its validated direct native code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query, Il2CppMethodInfoLayout layout)
    {
        ThrowIfDisposed();
        return _session.ResolveMethodCode(query, layout);
    }

    /// <summary>Resolves a managed field from its declaring type and field name.</summary>
    /// <param name="query">The semantic field query to resolve.</param>
    /// <returns>The resolved runtime field and storage category.</returns>
    public ResolvedField ResolveField(FieldQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveField(query);
    }

    /// <summary>
    /// Resolves concrete normal static-field storage using the globally selected class layout when configured; otherwise the resolver prefers the public IL2CPP runtime storage API and falls back to a previously detected layout.
    /// Thread-static fields remain intentionally unsupported.
    /// </summary>
    /// <param name="query">The semantic field query identifying the requested normal static field.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveFieldStorage(query);
    }

    /// <summary>Resolves concrete normal static-field storage using one explicit class layout without changing the globally selected session layout.</summary>
    /// <param name="query">The semantic field query identifying the requested normal static field.</param>
    /// <param name="layout">The one-shot Il2CppClass layout override.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, Il2CppClassLayout layout)
    {
        ThrowIfDisposed();
        return _session.ResolveFieldStorage(query, layout);
    }

    /// <summary>
    /// Resolves concrete normal static-field storage after explicitly requesting automatic class-layout detection from multiple static-field queries.
    /// The detected layout is retained for later default calls until <see cref="ClearCache"/> invalidates automatic evidence.
    /// </summary>
    /// <param name="query">The semantic target field query.</param>
    /// <param name="layoutEvidenceQueries">Additional normal static fields used as independent structural evidence.</param>
    /// <returns>The validated concrete field-storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, IReadOnlyList<FieldQuery> layoutEvidenceQueries)
    {
        ThrowIfDisposed();
        return _session.ResolveFieldStorage(query, layoutEvidenceQueries);
    }

    /// <summary>
    /// Invalidates semantic results, runtime snapshots and automatically detected layouts while preserving explicitly selected layouts and registered compatibility candidates.
    /// Resolved entities created before this operation remain readable snapshots, but their navigation endpoints are intentionally invalidated and must be reacquired from the resolver.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();
        _session.ClearCache();
    }

    /// <summary>Releases the complete target-specific resolution session and its owned native process handle.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _session.Dispose();
        _disposed = true;
    }

    /// <summary>Throws when an operation is attempted after this public resolver has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
