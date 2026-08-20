using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Provides the public semantic entry point for resolving assemblies, types, methods and native method code inside a Windows x64 Unity IL2CPP process.
/// The resolver owns one internal resolution session and hides native process inspection, PE parsing, remote execution, runtime API traversal and compatibility-layout detection from consumers.
/// Queries remain semantic and independent from target-specific RVAs so integrations can resolve IL2CPP entities without depending on a particular game build.
/// </summary>
public sealed class Il2CppResolver : IDisposable
{
    /// <summary>
    /// Defines the default maximum duration allowed for one individual remote IL2CPP runtime invocation.
    /// </summary>
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Owns the internal target-specific resolution session used by every public resolver operation.
    /// </summary>
    private readonly ResolutionSession _session;

    /// <summary>
    /// Indicates whether this public resolver has already released its owned resolution session.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Gets the operating system identifier of the target process attached to this resolver.
    /// </summary>
    public int ProcessId => _session.ProcessId;

    /// <summary>
    /// Initializes the public resolver around an already constructed internal resolution session.
    /// </summary>
    /// <param name="session">The target-specific session owned by this resolver instance.</param>
    private Il2CppResolver(ResolutionSession session)
    {
        _session = session;
    }

    /// <summary>
    /// Attaches to a Windows x64 Unity IL2CPP process using the default runtime call timeout.
    /// The returned resolver owns the process attachment and must be disposed when resolution is no longer required.
    /// </summary>
    /// <param name="processId">The operating system identifier of the target Unity process.</param>
    /// <returns>A fully initialized resolver attached to the requested target process.</returns>
    public static Il2CppResolver Attach(int processId)
    {
        return Attach(processId, DefaultCallTimeout);
    }

    /// <summary>
    /// Attaches to a Windows x64 Unity IL2CPP process using the specified timeout for individual remote runtime calls.
    /// The returned resolver owns the process attachment and must be disposed when resolution is no longer required.
    /// </summary>
    /// <param name="processId">The operating system identifier of the target Unity process.</param>
    /// <param name="callTimeout">The maximum duration allowed for each individual remote IL2CPP runtime invocation.</param>
    /// <returns>A fully initialized resolver attached to the requested target process.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="callTimeout"/> is zero or negative.
    /// </exception>
    public static Il2CppResolver Attach(int processId, TimeSpan callTimeout)
    {
        ResolutionSession session = ResolutionSession.Attach(processId, callTimeout);
        return new Il2CppResolver(session);
    }

    /// <summary>
    /// Resolves a loaded IL2CPP assembly from its semantic identity.
    /// </summary>
    /// <param name="query">The semantic assembly query to resolve.</param>
    /// <returns>The resolved assembly and its runtime identity.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveAssembly(query);
    }

    /// <summary>
    /// Resolves a managed type from its assembly, namespace and type name.
    /// </summary>
    /// <param name="query">The semantic type query to resolve.</param>
    /// <returns>The resolved runtime type and its containing assembly.</returns>
    public ResolvedType ResolveType(TypeQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveType(query);
    }

    /// <summary>
    /// Resolves a managed method from its declaring type, method name and ordered parameter type names.
    /// </summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The resolved runtime <c>MethodInfo</c> and verified semantic signature.</returns>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveMethod(query);
    }

    /// <summary>
    /// Resolves a managed method and maps its runtime identity to validated direct native executable code.
    /// Runtime <c>MethodInfo</c> layout detection is performed automatically when native code mapping is first requested.
    /// </summary>
    /// <param name="query">The semantic method query identifying the requested overload.</param>
    /// <returns>The resolved method together with its validated native code address and compatibility evidence.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query)
    {
        ThrowIfDisposed();
        return _session.ResolveMethodCode(query);
    }

    /// <summary>
    /// Releases the complete target-specific resolution session and its owned native process handle.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _session.Dispose();
    }

    /// <summary>
    /// Ensures that this resolver has not already released its target-specific resolution session.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the resolver has already been disposed.
    /// </exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}