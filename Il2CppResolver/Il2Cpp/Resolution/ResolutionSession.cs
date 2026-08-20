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
    /// Validates known <c>Il2CppClass</c> structural profiles against multiple independent normal static-field declarations before class static storage is interpreted.
    /// </summary>
    private readonly Il2CppClassLayoutDetector _classLayoutDetector;

    /// <summary>
    /// Stores the target-wide <c>Il2CppClass</c> compatibility layout after successful multi-class validation.
    /// A null value indicates that normal static-field storage has not yet established sufficient structural evidence.
    /// </summary>
    private Il2CppClassLayout? _classLayout;

    /// <summary>
    /// Defines the maximum duration allowed for each individual remote IL2CPP runtime invocation.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Stores the target-wide <c>MethodInfo</c> structural compatibility layout after successful automatic detection.
    /// A null value indicates that automatic native method-code mapping has not yet required layout detection.
    /// Explicitly supplied layouts do not modify this value.
    /// </summary>
    private Il2CppMethodInfoLayout? _methodInfoLayout;

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
    private ResolutionSession(TargetProcess process, Il2CppTarget target, Il2CppRuntime runtime, IIl2CppResolutionBackend backend, ResolutionCache cache, Il2CppMethodInfoLayoutDetector layoutDetector, Il2CppClassLayoutDetector classLayoutDetector, TimeSpan callTimeout)
    {
        _process = process;
        _target = target;
        _runtime = runtime;
        _backend = backend;
        _cache = cache;
        _layoutDetector = layoutDetector;
        _classLayoutDetector = classLayoutDetector;
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

            Il2CppMethodInfoLayout[] methodLayouts =
            {
                Il2CppMethodInfoLayout.DirectMethodPointerFirstX64
            };

            Il2CppMethodInfoLayoutDetector layoutDetector = new(target, methodLayouts, MinimumValidatedMethodCount);
            Il2CppClassLayout[] classLayouts =
            {
                Il2CppClassLayout.Class29_1X64,
                Il2CppClassLayout.Class29_2X64
            };

            Il2CppClassLayoutDetector classLayoutDetector = new(target, classLayouts, minimumValidatedClassCount: 3);

            return new ResolutionSession(process, target, runtime, backend, cache, layoutDetector, classLayoutDetector, callTimeout);
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
    /// Resolves a semantic method and maps its runtime <c>MethodInfo</c> to validated executable native code using an automatically detected structural layout.
    /// Layout detection is performed lazily from multiple methods of the declaring runtime type and the resulting target-wide profile is reused for subsequent automatic mappings.
    /// </summary>
    /// <param name="query">The complete semantic method signature whose native implementation should be resolved.</param>
    /// <returns>The resolved semantic method together with its validated direct native code address.</returns>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);

        ResolvedMethod method = _backend.ResolveMethod(query);
        Il2CppMethodInfoLayout layout = GetMethodInfoLayout(method.DeclaringType);

        return ResolveMethodCode(method, layout);
    }

    /// <summary>
    /// Resolves a semantic method and maps its runtime <c>MethodInfo</c> to native executable code using the exact structural layout explicitly supplied by the caller.
    /// This path bypasses automatic <c>MethodInfo</c> layout detection entirely and does not modify any layout previously detected for the current session.
    /// The candidate native address remains subject to all normal GameAssembly and executable-section validations.
    /// </summary>
    /// <param name="query">The complete semantic method signature whose native implementation should be resolved.</param>
    /// <param name="layout">The explicit <c>MethodInfo</c> structural layout used to locate the direct method pointer.</param>
    /// <returns>The resolved semantic method together with its validated direct native code address.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> or <paramref name="layout"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the supplied structural layout produces a null, out-of-image or non-executable native method pointer.
    /// </exception>
    public ResolvedMethodCode ResolveMethodCode(MethodQuery query, Il2CppMethodInfoLayout layout)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(layout);

        ResolvedMethod method = _backend.ResolveMethod(query);

        return ResolveMethodCode(method, layout);
    }

    /// <summary>
    /// Maps an already resolved semantic method to validated native code through the specified structural compatibility layout.
    /// Compatible results are reused from the session cache when available.
    /// </summary>
    /// <param name="method">The semantically resolved runtime method.</param>
    /// <param name="layout">The explicit or automatically detected <c>MethodInfo</c> layout.</param>
    /// <returns>The validated native method-code mapping.</returns>
    private ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout)
    {
        if (_cache.TryGetMethodCode(method.MethodInfoAddress, layout, out ResolvedMethodCode? cachedCode))
            return cachedCode;

        Il2CppMethodPointerResolver pointerResolver = new(_target, layout);
        ResolvedMethodCode result = pointerResolver.Resolve(method);

        _cache.StoreMethodCode(result, layout);

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
    private Il2CppMethodInfoLayout GetMethodInfoLayout(ResolvedType evidenceType)
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
    /// Resolves concrete normal static-field storage using the class-layout profile already detected for the current target session.
    /// </summary>
    /// <param name="query">The semantic field query identifying the requested normal static field.</param>
    /// <returns>The validated normal static-field storage mapping.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no class-layout compatibility profile has yet been detected for this session.
    /// </exception>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query)
    {
        ThrowIfDisposed();

        if (_classLayout is null)
            throw new InvalidOperationException("No IL2CPP class layout has been selected. Supply an explicit layout or provide evidence for automatic layout detection.");

        ResolvedField field = _backend.ResolveField(query);

        return ResolveFieldStorage(field, _classLayout);
    }

    /// <summary>
    /// Resolves concrete normal static-field storage while automatically detecting the target <c>Il2CppClass</c> layout from multiple independent static-field declarations when necessary.
    /// Once a unique profile has been validated, it is retained for subsequent static-field storage resolutions in the same session.
    /// </summary>
    /// <param name="query">The semantic field query identifying the requested normal static field.</param>
    /// <param name="layoutEvidenceQueries">Additional normal static fields belonging to independent declaring classes and used as layout evidence.</param>
    /// <returns>The validated normal static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, IReadOnlyList<FieldQuery> layoutEvidenceQueries)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(layoutEvidenceQueries);

        ResolvedField field = _backend.ResolveField(query);

        Il2CppClassLayout layout = GetClassLayout(field, layoutEvidenceQueries);

        return ResolveFieldStorage(field, layout);
    }

    /// <summary>
    /// Resolves concrete normal static-field storage using the exact <c>Il2CppClass</c> layout explicitly supplied by the caller.
    /// This path bypasses automatic class-layout detection entirely and does not modify any layout previously detected for the session.
    /// The supplied layout is still subject to all pointer, size, offset and memory-readability validations performed by <see cref="Il2CppStaticFieldStorageResolver"/>.
    /// </summary>
    /// <param name="query">The semantic field query identifying the requested normal static field.</param>
    /// <param name="layout">The explicit structural layout used to interpret the declaring <c>Il2CppClass</c>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="query"/> or <paramref name="layout"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the requested field uses thread-static storage.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested field is not a normal static field.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the explicit layout produces inconsistent or unreadable runtime storage information.
    /// </exception>
    public ResolvedFieldStorage ResolveFieldStorage(FieldQuery query, Il2CppClassLayout layout)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(layout);

        ResolvedField field = _backend.ResolveField(query);

        return ResolveFieldStorage(field, layout);
    }


    /// <summary>
    /// Maps an already resolved normal static field to concrete process storage through the specified class-layout profile.
    /// Compatible mappings are reused from the session cache when available.
    /// </summary>
    /// <param name="field">The semantically resolved normal static field.</param>
    /// <param name="layout">The explicit or automatically detected <c>Il2CppClass</c> compatibility profile.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    private ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout)
    {
        if (_cache.TryGetFieldStorage(field, layout, out ResolvedFieldStorage? cachedStorage))
            return cachedStorage;

        Il2CppStaticFieldStorageResolver storageResolver = new(_target, layout);
        ResolvedFieldStorage storage = storageResolver.Resolve(field);

        _cache.StoreFieldStorage(storage, layout);

        return storage;
    }

    /// <summary>
    /// Ensures that a unique target-wide <c>Il2CppClass</c> layout has been detected before normal static-field storage is interpreted.
    /// The target field itself contributes evidence and additional supplied static-field queries must provide enough distinct declaring classes to satisfy the detector confidence threshold.
    /// </summary>
    /// <param name="targetField">The normal static field whose storage will subsequently be resolved.</param>
    /// <param name="evidenceQueries">Additional semantic static-field queries used to establish independent class-layout evidence.</param>
    /// <returns>The unique validated <c>Il2CppClass</c> compatibility profile.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="targetField"/> or <paramref name="evidenceQueries"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the supplied fields do not provide enough independent evidence or when no unique compatibility profile survives validation.
    /// </exception>
    private Il2CppClassLayout GetClassLayout(ResolvedField targetField, IReadOnlyList<FieldQuery> evidenceQueries)
    {
        if (_classLayout is not null)
            return _classLayout;

        ArgumentNullException.ThrowIfNull(targetField);
        ArgumentNullException.ThrowIfNull(evidenceQueries);

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

        Il2CppClassLayoutDetectionResult detection = _classLayoutDetector.Detect(evidence);

        _classLayout = detection.Layout;

        return _classLayout;
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
        _classLayout = null;
    }
}