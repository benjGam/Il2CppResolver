using RuntimeClassInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassInfo;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Maintains session-scoped type descriptors, class metadata and class-relationship snapshots keyed by native IL2CPP identity.
/// The catalogue keeps optional type introspection lazy so targeted semantic resolution does not pay metadata or relationship traversal costs until explicitly requested.
/// </summary>
internal sealed class Il2CppTypeCatalog
{
    /// <summary>Provides live runtime calls used when a requested type entry has not yet been materialized.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Defines the timeout applied to each individual remote runtime call.</summary>
    private readonly TimeSpan _callTimeout;
    /// <summary>Stores field type descriptors indexed by native <c>Il2CppType*</c> identity.</summary>
    private readonly Dictionary<nint, RuntimeTypeDescriptor> _typeDescriptors = new();
    /// <summary>Stores complete class metadata indexed by native <c>Il2CppClass*</c> identity.</summary>
    private readonly Dictionary<nint, RuntimeClassMetadata> _classMetadata = new();
    /// <summary>Stores lightweight class semantic descriptions used by relationship materialization.</summary>
    private readonly Dictionary<nint, RuntimeClassInfo> _classInfos = new();
    /// <summary>Stores value-type classification independently from complete metadata capability.</summary>
    private readonly Dictionary<nint, bool> _valueTypeFlags = new();
    /// <summary>Stores instance sizes used to validate instance-field bounds.</summary>
    private readonly Dictionary<nint, int> _instanceSizes = new();
    /// <summary>Stores parent class identities, including explicit zero values for root types.</summary>
    private readonly Dictionary<nint, nint> _parents = new();
    /// <summary>Stores declaring class identities, including explicit zero values for top-level types.</summary>
    private readonly Dictionary<nint, nint> _declaringTypes = new();
    /// <summary>Stores interface snapshots indexed by declaring class identity.</summary>
    private readonly Dictionary<nint, IReadOnlyList<nint>> _interfaces = new();
    /// <summary>Stores nested-type snapshots indexed by declaring class identity.</summary>
    private readonly Dictionary<nint, IReadOnlyList<nint>> _nestedTypes = new();
    /// <summary>Stores canonical array element type identities indexed by array-class identity.</summary>
    private readonly Dictionary<nint, nint> _arrayElementTypes = new();
    /// <summary>Stores native array element sizes indexed by array-class identity.</summary>
    private readonly Dictionary<nint, int> _arrayElementSizes = new();

    /// <summary>Initializes a session-scoped type catalogue.</summary>
    /// <param name="runtime">The live IL2CPP runtime used to materialize missing entries.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppTypeCatalog(Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _callTimeout = callTimeout;
    }

    /// <summary>Gets the cached or freshly inspected descriptor for one native IL2CPP type.</summary>
    /// <param name="typeAddress">The native <c>Il2CppType*</c> identity.</param>
    /// <returns>The complete field-value interpretation descriptor.</returns>
    public RuntimeTypeDescriptor GetTypeDescriptor(nint typeAddress)
    {
        if (_typeDescriptors.TryGetValue(typeAddress, out RuntimeTypeDescriptor? descriptor))
            return descriptor;

        descriptor = _runtime.GetTypeDescriptor(typeAddress, _callTimeout);
        _typeDescriptors.Add(typeAddress, descriptor);

        if (descriptor.ClassAddress != 0)
            _valueTypeFlags[descriptor.ClassAddress] = descriptor.IsValueType;

        return descriptor;
    }

    /// <summary>Gets complete public metadata for one runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> identity.</param>
    /// <returns>The cached or freshly inspected class metadata.</returns>
    public RuntimeClassMetadata GetClassMetadata(nint classAddress)
    {
        if (_classMetadata.TryGetValue(classAddress, out RuntimeClassMetadata? metadata))
            return metadata;

        metadata = _runtime.GetClassMetadata(classAddress, _callTimeout);
        _classMetadata.Add(classAddress, metadata);
        _valueTypeFlags[classAddress] = metadata.IsValueType;
        return metadata;
    }

    /// <summary>Gets whether one runtime class is a managed value type without requiring complete class metadata.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> identity.</param>
    /// <returns><see langword="true"/> when IL2CPP reports the class as a value type.</returns>
    public bool IsClassValueType(nint classAddress)
    {
        if (_valueTypeFlags.TryGetValue(classAddress, out bool isValueType))
            return isValueType;

        isValueType = _runtime.IsClassValueType(classAddress, _callTimeout);
        _valueTypeFlags.Add(classAddress, isValueType);
        return isValueType;
    }

    /// <summary>Gets the total native instance size reported by IL2CPP for one class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> identity.</param>
    /// <returns>The total instance size in bytes.</returns>
    public int GetClassInstanceSize(nint classAddress)
    {
        if (_instanceSizes.TryGetValue(classAddress, out int size))
            return size;

        size = _runtime.GetClassInstanceSize(classAddress, _callTimeout);
        _instanceSizes.Add(classAddress, size);
        return size;
    }

    /// <summary>Gets the semantic runtime description of a class used to materialize related public types.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> identity.</param>
    /// <returns>The cached or freshly inspected class description.</returns>
    public RuntimeClassInfo GetClassInfo(nint classAddress)
    {
        if (_classInfos.TryGetValue(classAddress, out RuntimeClassInfo? info))
            return info;

        info = _runtime.GetClassInfo(classAddress, _callTimeout);
        _classInfos.Add(classAddress, info);
        return info;
    }

    /// <summary>Gets the parent class identity, or zero for a root type.</summary>
    /// <param name="classAddress">The native class whose parent should be retrieved.</param>
    /// <returns>The parent <c>Il2CppClass*</c>, or zero.</returns>
    public nint GetParent(nint classAddress)
    {
        if (_parents.TryGetValue(classAddress, out nint parent))
            return parent;

        parent = _runtime.GetClassParent(classAddress, _callTimeout);
        _parents.Add(classAddress, parent);
        return parent;
    }

    /// <summary>Gets the declaring class identity, or zero for a top-level type.</summary>
    /// <param name="classAddress">The native class whose declaring type should be retrieved.</param>
    /// <returns>The declaring <c>Il2CppClass*</c>, or zero.</returns>
    public nint GetDeclaringType(nint classAddress)
    {
        if (_declaringTypes.TryGetValue(classAddress, out nint declaringType))
            return declaringType;

        declaringType = _runtime.GetClassDeclaringType(classAddress, _callTimeout);
        _declaringTypes.Add(classAddress, declaringType);
        return declaringType;
    }

    /// <summary>Gets the cached or freshly enumerated interface class identities.</summary>
    /// <param name="classAddress">The declaring class whose interfaces should be enumerated.</param>
    /// <returns>The immutable interface class-address snapshot.</returns>
    public IReadOnlyList<nint> GetInterfaces(nint classAddress)
    {
        if (_interfaces.TryGetValue(classAddress, out IReadOnlyList<nint>? interfaces))
            return interfaces;

        interfaces = _runtime.GetClassInterfaces(classAddress, _callTimeout);
        _interfaces.Add(classAddress, interfaces);
        return interfaces;
    }

    /// <summary>Gets the cached or freshly enumerated nested class identities.</summary>
    /// <param name="classAddress">The declaring class whose nested types should be enumerated.</param>
    /// <returns>The immutable nested class-address snapshot.</returns>
    public IReadOnlyList<nint> GetNestedTypes(nint classAddress)
    {
        if (_nestedTypes.TryGetValue(classAddress, out IReadOnlyList<nint>? nestedTypes))
            return nestedTypes;

        nestedTypes = _runtime.GetClassNestedTypes(classAddress, _callTimeout);
        _nestedTypes.Add(classAddress, nestedTypes);
        return nestedTypes;
    }

    /// <summary>Gets the canonical element type represented by one single-dimensional array class.</summary>
    /// <param name="arrayClassAddress">The native array <c>Il2CppClass*</c> identity.</param>
    /// <returns>The canonical element <c>Il2CppType*</c> address.</returns>
    public nint GetArrayElementTypeAddress(nint arrayClassAddress)
    {
        if (_arrayElementTypes.TryGetValue(arrayClassAddress, out nint typeAddress))
            return typeAddress;

        nint elementClassAddress = _runtime.GetArrayElementClass(arrayClassAddress, _callTimeout);
        typeAddress = _runtime.GetClassType(elementClassAddress, _callTimeout);
        _arrayElementTypes.Add(arrayClassAddress, typeAddress);
        return typeAddress;
    }

    /// <summary>Gets the native element size represented by one array class.</summary>
    /// <param name="arrayClassAddress">The native array <c>Il2CppClass*</c> identity.</param>
    /// <returns>The positive native element size in bytes.</returns>
    public int GetArrayElementSize(nint arrayClassAddress)
    {
        if (_arrayElementSizes.TryGetValue(arrayClassAddress, out int size))
            return size;

        size = _runtime.GetArrayElementSize(arrayClassAddress, _callTimeout);
        _arrayElementSizes.Add(arrayClassAddress, size);
        return size;
    }

    /// <summary>Clears every lazily materialized type, metadata and relationship snapshot.</summary>
    public void Clear()
    {
        _typeDescriptors.Clear();
        _classMetadata.Clear();
        _classInfos.Clear();
        _valueTypeFlags.Clear();
        _instanceSizes.Clear();
        _parents.Clear();
        _declaringTypes.Clear();
        _interfaces.Clear();
        _nestedTypes.Clear();
        _arrayElementTypes.Clear();
        _arrayElementSizes.Clear();
    }
}
