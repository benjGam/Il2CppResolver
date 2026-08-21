using UnityIl2CppResolver.Il2Cpp.Navigation;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;
using UnityIl2CppResolver.Il2Cpp.Values;
using UnityIl2CppResolver.Native.Memory;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeManagedInvocationResult = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppManagedInvocationResult;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Invocation;

/// <summary>
/// Executes parameterless IL2CPP property getters through <c>il2cpp_runtime_invoke</c> and converts their managed results through the resolver's existing validated value-reading infrastructure.
/// Instance invocation validates object readability, runtime class assignability and virtual dispatch before managed execution; value-type instances, indexers, setters and arbitrary method arguments remain intentionally outside this layer.
/// </summary>
internal sealed class Il2CppGetterInvoker
{
    /// <summary>Provides read-only validation and reconstruction of unboxed value payloads.</summary>
    private readonly ProcessMemory _memory;
    /// <summary>Provides the IL2CPP embedding APIs required for managed invocation and object inspection.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Provides the cached active domain and other runtime snapshots.</summary>
    private readonly Il2CppRuntimeCatalog _runtimeCatalog;
    /// <summary>Provides cached return-type descriptors and class metadata.</summary>
    private readonly Il2CppTypeCatalog _types;
    /// <summary>Decodes managed string return objects.</summary>
    private readonly Il2CppStringReader _strings;
    /// <summary>Materializes validated array return objects.</summary>
    private readonly Il2CppArrayReader _arrays;
    /// <summary>Defines the maximum duration allowed for each individual runtime operation.</summary>
    private readonly TimeSpan _callTimeout;
    /// <summary>Maps managed object addresses exposed beyond an invocation to the single opaque pointer-sized strong GC handle retained for each object until cache invalidation or session disposal.</summary>
    private readonly Dictionary<nint, nuint> _retainedResultHandles = new();

    /// <summary>Initializes a getter invocation service for one resolver session.</summary>
    /// <param name="memory">The target process memory accessor.</param>
    /// <param name="runtime">The live IL2CPP runtime facade.</param>
    /// <param name="runtimeCatalog">The session-scoped runtime catalogue.</param>
    /// <param name="strings">The managed string reader.</param>
    /// <param name="arrays">The managed array reader.</param>
    /// <param name="callTimeout">The timeout applied to each runtime operation.</param>
    public Il2CppGetterInvoker(ProcessMemory memory, Il2CppRuntime runtime, Il2CppRuntimeCatalog runtimeCatalog, Il2CppStringReader strings, Il2CppArrayReader arrays, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(arrays);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _memory = memory;
        _runtime = runtime;
        _runtimeCatalog = runtimeCatalog;
        _types = runtimeCatalog.GetTypeCatalog();
        _strings = strings;
        _arrays = arrays;
        _callTimeout = callTimeout;
    }

    /// <summary>Invokes one getter and reads its boxed return as an explicitly supported scalar value.</summary>
    /// <typeparam name="T">The exact supported scalar type.</typeparam>
    /// <param name="property">The readable property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <returns>The validated scalar return value.</returns>
    public T Read<T>(ResolvedProperty property, nint instanceAddress, bool requireStatic) where T : unmanaged
    {
        RuntimeTypeDescriptor descriptor = GetReturnType(property);
        int size = FieldValueTypeValidator.ValidateScalar<T>(descriptor);
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);

        try
        {
            nint valueAddress = UnboxRequired(property, result.ReturnObjectAddress, size);
            return _memory.Read<T>(valueAddress);
        }
        finally
        {
            ReleaseResult(result);
        }
    }

    /// <summary>Invokes one getter and reads its boxed return as the exact managed enum type.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="property">The readable enum property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <returns>The validated enum return value.</returns>
    public TEnum ReadEnum<TEnum>(ResolvedProperty property, nint instanceAddress, bool requireStatic) where TEnum : unmanaged, Enum
    {
        RuntimeTypeDescriptor descriptor = GetReturnType(property);
        int size = FieldValueTypeValidator.ValidateEnum<TEnum>(descriptor);
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);

        try
        {
            nint valueAddress = UnboxRequired(property, result.ReturnObjectAddress, size);
            return _memory.Read<TEnum>(valueAddress);
        }
        finally
        {
            ReleaseResult(result);
        }
    }

    /// <summary>Invokes one getter and returns its managed-reference result without dereferencing the object.</summary>
    /// <param name="property">The readable reference property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <returns>The remote <c>Il2CppObject*</c>, or zero for a null return.</returns>
    public nint ReadReference(ResolvedProperty property, nint instanceAddress, bool requireStatic)
    {
        FieldValueTypeValidator.ValidateManagedReference(GetReturnType(property));
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);
        RetainResult(result);
        return result.ReturnObjectAddress;
    }

    /// <summary>Invokes one getter and decodes its <c>System.String</c> return value.</summary>
    /// <param name="property">The readable string property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadString(ResolvedProperty property, nint instanceAddress, bool requireStatic)
    {
        FieldValueTypeValidator.ValidateString(GetReturnType(property));
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);

        try
        {
            return _strings.Read(result.ReturnObjectAddress);
        }
        finally
        {
            ReleaseResult(result);
        }
    }

    /// <summary>Invokes one getter and materializes its single-dimensional zero-based managed array return value.</summary>
    /// <param name="property">The readable array property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <param name="navigator">The owning session navigator used by the returned array.</param>
    /// <param name="generation">The generation attached to the returned array.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null return.</returns>
    public ResolvedArray? ReadArray(ResolvedProperty property, nint instanceAddress, bool requireStatic, IResolutionNavigator navigator, long generation)
    {
        RuntimeTypeDescriptor descriptor = GetReturnType(property);
        FieldValueTypeValidator.ValidateVectorArray(descriptor);
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);

        if (result.ReturnObjectAddress == 0)
        {
            ReleaseResult(result);
            return null;
        }

        try
        {
            ResolvedArray array = _arrays.Resolve(result.ReturnObjectAddress, property.Getter!.RuntimeReturnTypeAddress, navigator, generation);
            RetainResult(result);
            return array;
        }
        catch
        {
            TryReleaseResult(result);
            throw;
        }
    }

    /// <summary>Invokes one getter and reads its boxed return as an explicitly validated blittable value type.</summary>
    /// <typeparam name="T">The unmanaged managed structure matching the IL2CPP return type.</typeparam>
    /// <param name="property">The readable value-type property.</param>
    /// <param name="instanceAddress">The instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the caller explicitly requested a static getter.</param>
    /// <returns>The raw blittable return value reconstructed from the boxed payload.</returns>
    public T ReadBlittable<T>(ResolvedProperty property, nint instanceAddress, bool requireStatic) where T : unmanaged
    {
        RuntimeTypeDescriptor descriptor = GetReturnType(property);

        if (descriptor.ClassAddress == 0)
            throw new InvalidDataException($"IL2CPP property return type '{descriptor.TypeName}' does not expose a runtime class identity.");

        RuntimeClassMetadata metadata = _types.GetClassMetadata(descriptor.ClassAddress);
        int size = FieldValueTypeValidator.ValidateBlittable<T>(descriptor, metadata);
        RuntimeManagedInvocationResult result = Invoke(property, instanceAddress, requireStatic);

        try
        {
            nint valueAddress = UnboxRequired(property, result.ReturnObjectAddress, size);
            return _memory.Read<T>(valueAddress);
        }
        finally
        {
            ReleaseResult(result);
        }
    }

    /// <summary>Gets the validated return type descriptor of one readable parameterless property getter.</summary>
    /// <param name="property">The resolved property whose getter return type should be inspected.</param>
    /// <returns>The cached runtime return type descriptor.</returns>
    private RuntimeTypeDescriptor GetReturnType(ResolvedProperty property)
    {
        ResolvedMethod getter = GetGetter(property);
        return _types.GetTypeDescriptor(getter.RuntimeReturnTypeAddress);
    }

    /// <summary>Validates one property getter and returns its resolved method.</summary>
    /// <param name="property">The property to validate.</param>
    /// <returns>The readable parameterless getter.</returns>
    private ResolvedMethod GetGetter(ResolvedProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        ResolvedMethod getter = property.Getter ?? throw new InvalidOperationException($"Property '{property.Query.Name}' does not expose a getter.");

        if (getter.ParameterTypeNames.Count != 0)
            throw new NotSupportedException($"Property '{property.Query.Name}' has {getter.ParameterTypeNames.Count} getter parameter(s). Indexer and parameterized-property invocation is intentionally unsupported.");

        if (_runtime.IsMethodGeneric(getter.MethodInfoAddress, _callTimeout))
            throw new NotSupportedException($"Property '{property.Query.Name}' uses a generic getter. Generic getter invocation is intentionally unsupported.");

        return getter;
    }

    /// <summary>Releases all strong getter-result handles retained for raw reference or array results. Cleanup is best effort; failed releases are intentionally abandoned rather than retried because completion may be unknown.</summary>
    public void ClearRetainedResults()
    {
        nuint[] handles = _retainedResultHandles.Values.ToArray();
        _retainedResultHandles.Clear();

        foreach (nuint handle in handles)
        {
            try
            {
                _runtime.ReleaseGcHandle(handle, _callTimeout);
            }
            catch (Exception)
            {
                // A failed or timed-out cleanup must not be retried because the target may already have released the handle.
            }
        }
    }

    /// <summary>Performs one validated static or instance getter invocation and returns the rooted managed result.</summary>
    /// <param name="property">The readable property.</param>
    /// <param name="instanceAddress">The requested instance address, or zero for static invocation.</param>
    /// <param name="requireStatic">Whether the public call explicitly requested static invocation.</param>
    /// <returns>The managed invocation result together with the strong GC handle retaining any non-null return object.</returns>
    private RuntimeManagedInvocationResult Invoke(ResolvedProperty property, nint instanceAddress, bool requireStatic)
    {
        ResolvedMethod getter = GetGetter(property);
        bool isInstance = _runtime.IsMethodInstance(getter.MethodInfoAddress, _callTimeout);

        if (requireStatic && isInstance)
            throw new InvalidOperationException($"Property '{property.Query.Name}' uses an instance getter and cannot be read through a static-property API.");

        if (!requireStatic && !isInstance)
            throw new InvalidOperationException($"Property '{property.Query.Name}' uses a static getter and cannot be read through an instance-property API.");

        nint invocationMethod = getter.MethodInfoAddress;

        if (isInstance)
        {
            ValidateInstance(property, instanceAddress);
            invocationMethod = _runtime.GetVirtualMethod(instanceAddress, getter.MethodInfoAddress, _callTimeout);
        }
        else if (instanceAddress != 0)
            throw new InvalidOperationException("Static getter invocation must not supply a managed instance.");

        RuntimeManagedInvocationResult result = _runtime.InvokeParameterlessMethod(_runtimeCatalog.GetDomainAddress(), invocationMethod, isInstance ? instanceAddress : 0, _callTimeout);

        if (result.ExceptionAddress != 0)
        {
            TryReleaseResult(result);
            throw new Il2CppInvocationException($"IL2CPP getter '{property.DeclaringType.Query.Namespace}.{property.DeclaringType.Query.Name}.{property.Query.Name}' raised a managed exception at 0x{result.ExceptionAddress:X}.", result.ExceptionAddress);
        }

        return result;
    }

    /// <summary>Retains one non-null getter result for APIs that expose a remote reference beyond the invocation call.</summary>
    /// <param name="result">The rooted invocation result to retain until cache invalidation or disposal.</param>
    private void RetainResult(RuntimeManagedInvocationResult result)
    {
        if (result.ReturnObjectAddress != 0 && result.ReturnObjectGcHandle == 0)
            throw new InvalidDataException("IL2CPP returned a managed object without the strong GC handle required to preserve its lifetime.");

        if (result.ReturnObjectGcHandle == 0)
            return;

        if (_retainedResultHandles.ContainsKey(result.ReturnObjectAddress))
        {
            TryReleaseResult(result);
            return;
        }

        _retainedResultHandles.Add(result.ReturnObjectAddress, result.ReturnObjectGcHandle);
    }

    /// <summary>Releases the strong GC handle associated with one transient invocation result.</summary>
    /// <param name="result">The invocation result whose temporary root is no longer needed.</param>
    private void ReleaseResult(RuntimeManagedInvocationResult result)
    {
        if (result.ReturnObjectGcHandle != 0)
            _runtime.ReleaseGcHandle(result.ReturnObjectGcHandle, _callTimeout);
    }

    /// <summary>Attempts to release one temporary result root without allowing cleanup failure to replace an already established primary result or exception.</summary>
    /// <param name="result">The invocation result whose temporary root should be released on a best-effort basis.</param>
    private void TryReleaseResult(RuntimeManagedInvocationResult result)
    {
        try
        {
            ReleaseResult(result);
        }
        catch (Exception)
        {
            // Cleanup cannot be retried safely because a timed-out target call may already have released the handle.
        }
    }

    /// <summary>Validates a managed object before it is supplied to an instance property getter.</summary>
    /// <param name="property">The property declaring the expected runtime class.</param>
    /// <param name="instanceAddress">The non-null managed object address.</param>
    private void ValidateInstance(ResolvedProperty property, nint instanceAddress)
    {
        if (instanceAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(instanceAddress), "An instance property getter requires a non-null Il2CppObject pointer.");

        if (_types.IsClassValueType(property.DeclaringType.ClassAddress))
            throw new NotSupportedException($"Instance property getters declared by value type '{property.DeclaringType.Query.Name}' are intentionally unsupported in this invocation version.");

        nuint objectHeaderSize = checked((nuint)(IntPtr.Size * 2));

        if (!_memory.IsReadableRange(instanceAddress, objectHeaderSize))
            throw new InvalidDataException($"Managed object header at 0x{instanceAddress:X} is not completely readable.");

        nint objectClass = _runtime.GetObjectClass(instanceAddress, _callTimeout);

        if (!_memory.IsReadableRange(objectClass, (nuint)IntPtr.Size))
            throw new InvalidDataException($"Runtime class 0x{objectClass:X} returned for object 0x{instanceAddress:X} is not readable.");

        if (!_runtime.IsClassAssignableFrom(property.DeclaringType.ClassAddress, objectClass, _callTimeout))
            throw new InvalidOperationException($"Object 0x{instanceAddress:X} is not assignable to property declaring type '{property.DeclaringType.Query.Namespace}.{property.DeclaringType.Query.Name}'.");
    }

    /// <summary>Unboxes one required value-type result and validates the complete returned payload range before local reconstruction.</summary>
    /// <param name="property">The property whose getter produced the boxed result.</param>
    /// <param name="returnObject">The non-null boxed result object.</param>
    /// <param name="size">The exact payload size that will be read.</param>
    /// <returns>The validated raw boxed-value payload address.</returns>
    private nint UnboxRequired(ResolvedProperty property, nint returnObject, int size)
    {
        if (returnObject == 0)
            throw new InvalidDataException($"IL2CPP getter for value property '{property.Query.Name}' returned a null boxed object.");

        nint valueAddress = _runtime.UnboxObject(returnObject, _callTimeout);

        if (!_memory.IsReadableRange(valueAddress, checked((nuint)size)))
            throw new InvalidDataException($"Unboxed return payload at 0x{valueAddress:X} with size 0x{size:X} is not completely readable.");

        return valueAddress;
    }
}
