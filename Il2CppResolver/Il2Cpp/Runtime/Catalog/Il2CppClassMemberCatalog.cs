using RuntimeFieldInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppFieldInfo;
using RuntimeMethodInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodInfo;
using RuntimePropertyInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppPropertyInfo;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Catalog;

/// <summary>
/// Stores a lazy local snapshot of methods, fields and properties declared by one runtime <c>Il2CppClass</c>.
/// Enumeration and member-name inspection occur at most once per session snapshot, while complete member descriptions are loaded only for candidates that require semantic inspection.
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

    /// <summary>Stores the raw property-address snapshot after the class property iterator has been consumed once.</summary>
    private IReadOnlyList<nint>? _propertyAddresses;

    /// <summary>Stores property addresses grouped by exact semantic name after the property-name index has been built once.</summary>
    private Dictionary<string, List<nint>>? _propertiesByName;

    /// <summary>Stores property names indexed by native PropertyInfo identity after property-name indexing.</summary>
    private readonly Dictionary<nint, string> _propertyNames = new();

    /// <summary>
    /// Stores complete runtime method descriptions already required by semantic overload resolution.
    /// </summary>
    private readonly Dictionary<nint, RuntimeMethodInfo> _methodInfos = new();

    /// <summary>
    /// Stores complete runtime field descriptions already required by semantic field resolution.
    /// </summary>
    private readonly Dictionary<nint, RuntimeFieldInfo> _fieldInfos = new();


    /// <summary>Stores complete runtime property descriptions already required by semantic property resolution.</summary>
    private readonly Dictionary<nint, RuntimePropertyInfo> _propertyInfos = new();

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

    /// <summary>Gets every property address declared by this class without forcing accessor signature inspection.</summary>
    /// <returns>An immutable snapshot of the declared <c>PropertyInfo*</c> addresses.</returns>
    public IReadOnlyList<nint> GetPropertyAddresses()
    {
        if (!_runtime.Capabilities.CanEnumerateProperties)
            throw new NotSupportedException("The target IL2CPP runtime does not expose the complete property navigation capability.");

        if (_propertyAddresses is null)
            _propertyAddresses = _runtime.GetProperties(_classAddress, _callTimeout);

        return _propertyAddresses;
    }

    /// <summary>Gets property candidates whose exact runtime name matches the requested name.</summary>
    /// <param name="name">The exact managed property name.</param>
    /// <returns>The candidate <c>PropertyInfo*</c> addresses, or an empty list when no property has that name.</returns>
    public IReadOnlyList<nint> FindProperties(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsurePropertyIndex();
        return _propertiesByName!.TryGetValue(name, out List<nint>? properties) ? properties : Array.Empty<nint>();
    }

    /// <summary>Gets complete runtime descriptions for every property declared by this class.</summary>
    /// <returns>Every declared property with semantic type, index signature and accessor identities.</returns>
    public IReadOnlyList<RuntimePropertyInfo> GetProperties()
    {
        IReadOnlyList<nint> addresses = GetPropertyAddresses();
        List<RuntimePropertyInfo> properties = new(addresses.Count);

        foreach (nint address in addresses)
            properties.Add(GetPropertyInfo(address));

        return properties.AsReadOnly();
    }

    /// <summary>Gets complete runtime descriptions for every property matching the exact requested name.</summary>
    /// <param name="name">The exact managed property name.</param>
    /// <returns>Every matching property with semantic type, index signature and accessor identities.</returns>
    public IReadOnlyList<RuntimePropertyInfo> GetProperties(string name)
    {
        IReadOnlyList<nint> addresses = FindProperties(name);
        List<RuntimePropertyInfo> properties = new(addresses.Count);

        foreach (nint address in addresses)
            properties.Add(GetPropertyInfo(address));

        return properties.AsReadOnly();
    }

    /// <summary>Gets a complete runtime property description, reusing previously inspected accessors and signatures when available.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> to inspect.</param>
    /// <returns>The cached or newly derived runtime property description.</returns>
    public RuntimePropertyInfo GetPropertyInfo(nint propertyAddress)
    {
        if (_propertyInfos.TryGetValue(propertyAddress, out RuntimePropertyInfo? property))
            return property;

        EnsurePropertyIndex();
        string name = _propertyNames.TryGetValue(propertyAddress, out string? knownName) ? knownName : _runtime.GetPropertyName(propertyAddress, _callTimeout);
        nint getterAddress = _runtime.GetPropertyGetterMethod(propertyAddress, _callTimeout);
        nint setterAddress = _runtime.GetPropertySetterMethod(propertyAddress, _callTimeout);

        if (getterAddress == 0 && setterAddress == 0)
            throw new InvalidDataException($"IL2CPP property '{name}' at 0x{propertyAddress:X} exposes neither a getter nor a setter method.");

        RuntimeMethodInfo? getter = getterAddress != 0 ? GetMethodInfo(getterAddress) : null;
        RuntimeMethodInfo? setter = setterAddress != 0 ? GetMethodInfo(setterAddress) : null;
        DerivePropertySignature(name, propertyAddress, getter, setter, out string typeName, out IReadOnlyList<string> indexParameterTypeNames);
        System.Reflection.PropertyAttributes attributes = _runtime.GetPropertyAttributes(propertyAddress, _callTimeout);
        property = new RuntimePropertyInfo(propertyAddress, name, typeName, indexParameterTypeNames, attributes, getterAddress, setterAddress);
        _propertyInfos.Add(propertyAddress, property);
        return property;
    }

    /// <summary>Builds the local property-name index exactly once for this class snapshot.</summary>
    private void EnsurePropertyIndex()
    {
        if (_propertiesByName is not null)
            return;

        Dictionary<string, List<nint>> index = new(StringComparer.Ordinal);
        IReadOnlyList<nint> properties = GetPropertyAddresses();

        foreach (nint propertyAddress in properties)
        {
            string name = _runtime.GetPropertyName(propertyAddress, _callTimeout);
            _propertyNames[propertyAddress] = name;

            if (!index.TryGetValue(name, out List<nint>? bucket))
            {
                bucket = new List<nint>();
                index.Add(name, bucket);
            }

            bucket.Add(propertyAddress);
        }

        _propertiesByName = index;
    }

    /// <summary>Derives one canonical property signature from its getter and/or setter method descriptions and validates accessor consistency.</summary>
    /// <param name="propertyName">The semantic property name used in diagnostics.</param>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> identity used in diagnostics.</param>
    /// <param name="getter">The getter method description when present.</param>
    /// <param name="setter">The setter method description when present.</param>
    /// <param name="typeName">Receives the semantic property type name.</param>
    /// <param name="indexParameterTypeNames">Receives the ordered semantic index-parameter type names.</param>
    private static void DerivePropertySignature(string propertyName, nint propertyAddress, RuntimeMethodInfo? getter, RuntimeMethodInfo? setter, out string typeName, out IReadOnlyList<string> indexParameterTypeNames)
    {
        string? derivedTypeName = null;
        IReadOnlyList<string>? derivedIndexParameters = null;

        if (getter is not null)
        {
            if (string.Equals(getter.ReturnTypeName, "System.Void", StringComparison.Ordinal))
                throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} exposes a getter returning System.Void.");

            derivedTypeName = getter.ReturnTypeName;
            derivedIndexParameters = Array.AsReadOnly(getter.ParameterTypeNames.ToArray());
        }

        if (setter is not null)
        {
            if (setter.ParameterTypeNames.Count == 0)
                throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} exposes a setter without a value parameter.");

            if (!string.Equals(setter.ReturnTypeName, "System.Void", StringComparison.Ordinal))
                throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} exposes a setter returning '{setter.ReturnTypeName}' instead of System.Void.");

            string setterTypeName = setter.ParameterTypeNames[^1];
            string[] setterIndexParameters = setter.ParameterTypeNames.Take(setter.ParameterTypeNames.Count - 1).ToArray();

            if (derivedTypeName is not null && !string.Equals(derivedTypeName, setterTypeName, StringComparison.Ordinal))
                throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} exposes inconsistent getter and setter property types '{derivedTypeName}' and '{setterTypeName}'.");

            if (derivedIndexParameters is not null && !ParametersMatch(derivedIndexParameters, setterIndexParameters))
                throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} exposes inconsistent getter and setter index-parameter signatures.");

            derivedTypeName ??= setterTypeName;
            derivedIndexParameters ??= Array.AsReadOnly(setterIndexParameters);
        }

        typeName = derivedTypeName ?? throw new InvalidDataException($"IL2CPP property '{propertyName}' at 0x{propertyAddress:X} does not expose a derivable semantic type.");
        indexParameterTypeNames = derivedIndexParameters ?? Array.Empty<string>();
    }

    /// <summary>Determines whether two ordered semantic type-name sequences are identical.</summary>
    /// <param name="left">The first ordered type-name sequence.</param>
    /// <param name="right">The second ordered type-name sequence.</param>
    /// <returns><see langword="true"/> when both sequences contain the same names in the same order.</returns>
    private static bool ParametersMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                return false;
        }

        return true;
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
