using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Owns the complete native and IL2CPP state associated with one resolver attachment to a target process.
/// The session establishes the target, runtime API, semantic resolution backend and version-sensitive runtime-layout detection infrastructure while centralizing their lifetime under the owned <see cref="TargetProcess"/>.
/// This class is an internal orchestration boundary; public consumers interact through <see cref="Il2CppResolver"/> rather than manipulating native resolver components directly.
/// </summary>
internal sealed class ResolutionSession : IDisposable
{
    /// <summary>
    /// Defines the maximum number of methods sampled from a declaring type when runtime <c>MethodInfo</c> layout detection is first required.
    /// </summary>
    private const int MethodInfoLayoutSampleSize = 12;

    /// <summary>
    /// Defines the minimum number of executable native method pointers required before a runtime <c>MethodInfo</c> layout can be accepted.
    /// </summary>
    private const int MinimumValidatedMethodCount = 5;

    /// <summary>
    /// Owns the native process handle for the complete lifetime of this resolution session.
    /// Disposing the session disposes this object and invalidates every target-specific runtime entity associated with the session.
    /// </summary>
    private readonly TargetProcess _process;

    /// <summary>
    /// Represents the validated IL2CPP target discovered inside the owned process.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Provides low-level semantic access to the live IL2CPP runtime.
    /// The session uses this component both through the semantic backend and directly when runtime layout evidence is required.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Stores semantic and native resolution results produced during the lifetime of this target-specific session.
    /// The cache is owned by the session so no target address can survive attachment to the process that produced it.
    /// </summary>
    private readonly ResolutionCache _cache;

    /// <summary>
    /// Represents the active semantic resolution strategy used by the session.
    /// The initial implementation uses runtime introspection, while future sessions may compose metadata and fallback backends behind the same contract.
    /// </summary>
    private readonly IIl2CppResolutionBackend _backend;

    /// <summary>
    /// Validates known <c>MethodInfo</c> layouts against live runtime methods before native method pointers are interpreted.
    /// </summary>
    private readonly Il2CppMethodInfoLayoutDetector _layoutDetector;

    /// <summary>
    /// Defines the maximum duration allowed for each individual remote IL2CPP runtime invocation.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Stores the target-wide <c>MethodInfo</c> compatibility layout after it has been successfully detected.
    /// A null value indicates that native method-code mapping has not yet required layout detection.
    /// </summary>
    private IIl2CppMethodInfoLayout? _methodInfoLayout;

    /// <summary>
    /// Indicates whether this session has released ownership of its target process.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Gets the operating system identifier of the process owned by this resolution session.
    /// </summary>
    public int ProcessId => _process.ProcessId;

    /// <summary>
    /// Initializes a fully constructed resolution session from validated native and IL2CPP components.
    /// </summary>
    /// <param name="process">The owned target process.</param>
    /// <param name="target">The validated IL2CPP target discovered inside the process.</param>
    /// <param name="runtime">The low-level runtime introspection component.</param>
    /// <param name="backend">The semantic resolution backend used by the session.</param>
    /// <param name="layoutDetector">The runtime layout detector used for native method-code mapping.</param>
    /// <param name="callTimeout">The timeout applied to individual remote runtime calls.</param>
    private ResolutionSession(TargetProcess process, Il2CppTarget target, Il2CppRuntime runtime, IIl2CppResolutionBackend backend, ResolutionCache cache, Il2CppMethodInfoLayoutDetector layoutDetector, TimeSpan callTimeout)
    {
        _process = process;
        _target = target;
        _runtime = runtime;
        _backend = backend;
        _cache = cache;
        _layoutDetector = layoutDetector;
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Attaches to a Windows x64 process and initializes the complete IL2CPP runtime-resolution stack.
    /// If any discovery or initialization step fails, the newly opened target process handle is released before the exception is propagated.
    /// </summary>
    /// <param name="processId">The operating system identifier of the target Unity process.</param>
    /// <param name="callTimeout">The maximum duration allowed for each individual remote IL2CPP runtime invocation.</param>
    /// <returns>A fully initialized resolution session owning the target process.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="callTimeout"/> is zero or negative.
    /// </exception>
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
            ResolutionCache cache = new();
            RuntimeResolutionBackend backend = new(runtime, cache, callTimeout);

            IIl2CppMethodInfoLayout[] layouts =
            {
            DirectMethodPointerFirstLayout.Instance
        };

            Il2CppMethodInfoLayoutDetector layoutDetector = new(target, layouts, MinimumValidatedMethodCount);

            return new ResolutionSession(process, target, runtime, backend, cache, layoutDetector, callTimeout);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves an assembly through the active semantic backend.
    /// </summary>
    /// <param name="query">The semantic assembly identity to resolve.</param>
    /// <returns>The resolved IL2CPP assembly.</returns>
    public ResolvedAssembly ResolveAssembly(AssemblyQuery query)
    {
        ThrowIfDisposed();
        return _backend.ResolveAssembly(query);
    }

    /// <summary>
    /// Resolves a managed type through the active semantic backend.
    /// </summary>
    /// <param name="query">The semantic type identity to resolve.</param>
    /// <returns>The resolved IL2CPP type.</returns>
    public ResolvedType ResolveType(TypeQuery query)
    {
        ThrowIfDisposed();
        return _backend.ResolveType(query);
    }

    /// <summary>
    /// Resolves a managed method through the active semantic backend.
    /// </summary>
    /// <param name="query">The complete semantic method signature to resolve.</param>
    /// <returns>The resolved IL2CPP method.</returns>
    public ResolvedMethod ResolveMethod(MethodQuery query)
    {
        ThrowIfDisposed();
        return _backend.ResolveMethod(query);
    }

    /// <summary>
    /// Resolves a semantic method and maps its runtime <c>MethodInfo</c> to validated executable native code.
    /// Both semantic resolution and validated native mappings are reused from the session cache when available.
    /// </summary>
    /// <param name="query">The complete semantic method signature whose native implementation should be resolved.</param>
    /// <returns>The resolved semantic method together with its validated direct native code address.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query)
    {
        ThrowIfDisposed();

        ResolvedMethod method = _backend.ResolveMethod(query);

        if (_cache.TryGetMethodCode(method.MethodInfoAddress, out ResolvedMethodCode? cachedCode))
            return cachedCode;

        IIl2CppMethodInfoLayout layout = GetMethodInfoLayout(method.DeclaringType);
        Il2CppMethodPointerResolver pointerResolver = new(_target, layout);
        ResolvedMethodCode result = pointerResolver.Resolve(method);

        _cache.StoreMethodCode(result);

        return result;
    }

    /// <summary>
    /// Returns the validated target-wide <c>MethodInfo</c> layout, detecting it from multiple methods of the supplied resolved type when no layout has been established yet.
    /// </summary>
    /// <param name="evidenceType">A resolved runtime type whose declared methods provide structural layout evidence.</param>
    /// <returns>The unique compatibility layout accepted for the current target process.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the evidence type does not expose enough methods or when no unique compatibility profile can be validated.
    /// </exception>
    private IIl2CppMethodInfoLayout GetMethodInfoLayout(ResolvedType evidenceType)
    {
        if (_methodInfoLayout is not null)
            return _methodInfoLayout;

        IReadOnlyList<nint> availableMethods = _runtime.GetMethods(evidenceType.ClassAddress, _callTimeout);

        if (availableMethods.Count < MinimumValidatedMethodCount)
            throw new InvalidDataException($"Type '{evidenceType.Query.Namespace}.{evidenceType.Query.Name}' exposes only {availableMethods.Count} method(s), which is insufficient to validate the IL2CPP MethodInfo layout.");

        int sampleCount = Math.Min(availableMethods.Count, MethodInfoLayoutSampleSize);
        List<nint> sample = new(sampleCount);

        for (int index = 0; index < sampleCount; index++)
            sample.Add(availableMethods[index]);

        Il2CppMethodInfoLayoutDetectionResult detection = _layoutDetector.Detect(sample);

        _methodInfoLayout = detection.Layout;

        return _methodInfoLayout;
    }

    /// <summary>
    /// Resolves a managed field through the active semantic backend.
    /// </summary>
    /// <param name="query">The semantic field identity to resolve.</param>
    /// <returns>The resolved IL2CPP field and its storage characteristics.</returns>
    public ResolvedField ResolveField(FieldQuery query)
    {
        ThrowIfDisposed();
        return _backend.ResolveField(query);
    }

    /// <summary>
    /// Releases the target process owned by this resolution session.
    /// All runtime pointers and native resolution results associated with the session become invalid once the target process terminates or the session is disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _process.Dispose();
    }

    /// <summary>
    /// Ensures that the session still owns a usable target process before performing a resolution operation.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the session has already been disposed.
    /// </exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Invalidates every cached semantic and native resolution result associated with this session.
    /// The detected <c>MethodInfo</c> layout is also discarded so the next native method-code request rebuilds its compatibility evidence from the live target.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();

        _cache.Clear();
        _methodInfoLayout = null;
    }
}