using RuntimeFieldInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppFieldInfo;
using RuntimeMethodInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodInfo;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Stores a lazy local snapshot of methods and fields declared by one runtime <c>Il2CppClass</c>.
/// Enumeration and member-name inspection occur at most once per session snapshot, while complete method and field descriptions are loaded only for candidates that require semantic inspection.
/// </summary>
internal sealed class Il2CppClassMemberCatalog
{
    /// <summary>
    /// Provides the live IL2CPP operations used to populate this local member snapshot.
    /// </summary>
    private readonly Il2CppRuntime _runtime;

    /// <summary>
    /// Identifies the declaring runtime class represented by this catalogue.
    /// </summary>
    private readonly nint _classAddress;

    /// <summary>
    /// Defines the timeout applied to each remote runtime call required while populating the catalogue.
    /// </summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>
    /// Stores the raw method-address snapshot after the class method iterator has been consumed once.
    /// </summary>
    private IReadOnlyList<nint>? _methodAddresses;

    /// <summary>
    /// Stores method addresses grouped by exact semantic name after the method-name index has been built once.
    /// </summary>
    private Dictionary<string, List<nint>>? _methodsByName;

    /// <summary>Stores method names indexed by native MethodInfo identity after method-name indexing.</summary>
    private readonly Dictionary<nint, string> _methodNames = new();

    /// <summary>
    /// Stores the raw field-address snapshot after the class field iterator has been consumed once.
    /// </summary>
    private IReadOnlyList<nint>? _fieldAddresses;

    /// <summary>
    /// Stores field addresses grouped by exact semantic name after the field-name index has been built once.
    /// </summary>
    private Dictionary<string, List<nint>>? _fieldsByName;

    /// <summary>Stores field names indexed by native FieldInfo identity after field-name indexing.</summary>
    private readonly Dictionary<nint, string> _fieldNames = new();

    /// <summary>
    /// Stores complete runtime method descriptions already required by semantic overload resolution.
    /// </summary>
    private readonly Dictionary<nint, RuntimeMethodInfo> _methodInfos = new();

    /// <summary>
    /// Stores complete runtime field descriptions already required by semantic field resolution.
    /// </summary>
    private readonly Dictionary<nint, RuntimeFieldInfo> _fieldInfos = new();

    /// <summary>
    /// Initializes a class-member catalogue for one runtime class.
    /// </summary>
    /// <param name="runtime">The live IL2CPP runtime used to populate the snapshot.</param>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> represented by the catalogue.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppClassMemberCatalog(Il2CppRuntime runtime, nint classAddress, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class address cannot be zero.");

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _runtime = runtime;
        _classAddress = classAddress;
        _callTimeout = callTimeout;
    }

    /// <summary>
    /// Gets every method address declared by this class without forcing complete signature inspection.
    /// </summary>
    /// <returns>An immutable snapshot of the declared <c>MethodInfo*</c> addresses.</returns>
    public IReadOnlyList<nint> GetMethodAddresses()
    {
        if (_methodAddresses is null)
            _methodAddresses = _runtime.GetMethods(_classAddress, _callTimeout);

        return _methodAddresses;
    }

    /// <summary>
    /// Gets method candidates whose exact runtime name matches the requested name.
    /// </summary>
    /// <param name="name">The exact managed method name.</param>
    /// <returns>The candidate <c>MethodInfo*</c> addresses, or an empty list when no method has that name.</returns>
    public IReadOnlyList<nint> FindMethods(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureMethodIndex();
        return _methodsByName!.TryGetValue(name, out List<nint>? methods) ? methods : Array.Empty<nint>();
    }

    /// <summary>
    /// Gets a complete runtime method description, reusing a previously inspected signature when available.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> to inspect.</param>
    /// <returns>The cached or newly inspected runtime method description.</returns>
    public RuntimeMethodInfo GetMethodInfo(nint methodAddress)
    {
        if (_methodInfos.TryGetValue(methodAddress, out RuntimeMethodInfo? method))
            return method;

        EnsureMethodIndex();
        string name = _methodNames.TryGetValue(methodAddress, out string? knownName) ? knownName : _runtime.GetMethodName(methodAddress, _callTimeout);
        method = _runtime.GetMethodInfo(methodAddress, name, _callTimeout);
        _methodInfos.Add(methodAddress, method);
        return method;
    }

    /// <summary>
    /// Gets every field address declared by this class without forcing complete field inspection.
    /// </summary>
    /// <returns>An immutable snapshot of the declared <c>FieldInfo*</c> addresses.</returns>
    public IReadOnlyList<nint> GetFieldAddresses()
    {
        if (_fieldAddresses is null)
            _fieldAddresses = _runtime.GetFields(_classAddress, _callTimeout);

        return _fieldAddresses;
    }

    /// <summary>Gets complete runtime descriptions for every method declared by this class.</summary>
    /// <returns>Every declared method with its complete semantic signature.</returns>
    public IReadOnlyList<RuntimeMethodInfo> GetMethods()
    {
        IReadOnlyList<nint> addresses = GetMethodAddresses();
        List<RuntimeMethodInfo> methods = new(addresses.Count);

        foreach (nint address in addresses)
            methods.Add(GetMethodInfo(address));

        return methods.AsReadOnly();
    }

    /// <summary>Gets complete runtime descriptions for every overload matching the exact requested method name.</summary>
    /// <param name="name">The exact managed method name.</param>
    /// <returns>Every matching overload with its complete semantic signature.</returns>
    public IReadOnlyList<RuntimeMethodInfo> GetMethods(string name)
    {
        IReadOnlyList<nint> addresses = FindMethods(name);
        List<RuntimeMethodInfo> methods = new(addresses.Count);

        foreach (nint address in addresses)
            methods.Add(GetMethodInfo(address));

        return methods.AsReadOnly();
    }

    /// <summary>Gets complete runtime descriptions for every field declared by this class.</summary>
    /// <returns>Every declared field with semantic type, attributes and storage offset.</returns>
    public IReadOnlyList<RuntimeFieldInfo> GetFields()
    {
        IReadOnlyList<nint> addresses = GetFieldAddresses();
        List<RuntimeFieldInfo> fields = new(addresses.Count);

        foreach (nint address in addresses)
            fields.Add(GetFieldInfo(address));

        return fields.AsReadOnly();
    }

    /// <summary>
    /// Gets field candidates whose exact runtime name matches the requested name.
    /// </summary>
    /// <param name="name">The exact managed field name.</param>
    /// <returns>The candidate <c>FieldInfo*</c> addresses, or an empty list when no field has that name.</returns>
    public IReadOnlyList<nint> FindFields(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureFieldIndex();
        return _fieldsByName!.TryGetValue(name, out List<nint>? fields) ? fields : Array.Empty<nint>();
    }

    /// <summary>
    /// Gets a complete runtime field description, reusing a previously inspected field when available.
    /// </summary>
    /// <param name="fieldAddress">The native <c>FieldInfo*</c> to inspect.</param>
    /// <returns>The cached or newly inspected runtime field description.</returns>
    public RuntimeFieldInfo GetFieldInfo(nint fieldAddress)
    {
        if (_fieldInfos.TryGetValue(fieldAddress, out RuntimeFieldInfo? field))
            return field;

        EnsureFieldIndex();
        string name = _fieldNames.TryGetValue(fieldAddress, out string? knownName) ? knownName : _runtime.GetFieldName(fieldAddress, _callTimeout);
        field = _runtime.GetFieldInfo(fieldAddress, name, _callTimeout);
        _fieldInfos.Add(fieldAddress, field);
        return field;
    }

    /// <summary>
    /// Builds the local method-name index exactly once for this class snapshot.
    /// </summary>
    private void EnsureMethodIndex()
    {
        if (_methodsByName is not null)
            return;

        Dictionary<string, List<nint>> index = new(StringComparer.Ordinal);
        IReadOnlyList<nint> methods = GetMethodAddresses();

        foreach (nint methodAddress in methods)
        {
            string name = _runtime.GetMethodName(methodAddress, _callTimeout);
            _methodNames[methodAddress] = name;

            if (!index.TryGetValue(name, out List<nint>? bucket))
            {
                bucket = new List<nint>();
                index.Add(name, bucket);
            }

            bucket.Add(methodAddress);
        }

        _methodsByName = index;
    }

    /// <summary>
    /// Builds the local field-name index exactly once for this class snapshot.
    /// </summary>
    private void EnsureFieldIndex()
    {
        if (_fieldsByName is not null)
            return;

        Dictionary<string, List<nint>> index = new(StringComparer.Ordinal);

        IReadOnlyList<nint> fields = GetFieldAddresses();

        foreach (nint fieldAddress in fields)
        {
            string name = _runtime.GetFieldName(fieldAddress, _callTimeout);
            _fieldNames[fieldAddress] = name;

            if (!index.TryGetValue(name, out List<nint>? bucket))
            {
                bucket = new List<nint>();
                index.Add(name, bucket);
            }

            bucket.Add(fieldAddress);
        }

        _fieldsByName = index;
    }
}
