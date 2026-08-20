using System.Text;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Stores target-specific semantic and native resolution results for the lifetime of one resolver session cache generation.
/// Semantic queries, explicit structural mappings and runtime-API field-storage mappings use independent keys so incompatible evidence can never share a cached result.
/// </summary>
internal sealed class ResolutionCache
{
    /// <summary>Stores assembly resolution results indexed by exact semantic query identity.</summary>
    private readonly Dictionary<string, ResolvedAssembly> _assemblies = new(StringComparer.Ordinal);
    /// <summary>Stores type resolution results indexed by exact semantic query identity.</summary>
    private readonly Dictionary<string, ResolvedType> _types = new(StringComparer.Ordinal);
    /// <summary>Stores method resolution results indexed by exact semantic signature identity.</summary>
    private readonly Dictionary<string, ResolvedMethod> _methods = new(StringComparer.Ordinal);
    /// <summary>Stores field resolution results indexed by exact semantic identity.</summary>
    private readonly Dictionary<string, ResolvedField> _fields = new(StringComparer.Ordinal);

    /// <summary>
    /// Identifies one native method-code mapping by runtime method identity and exact structural profile evidence.
    /// </summary>
    private readonly record struct MethodCodeCacheKey(nint MethodInfoAddress, string CompatibilityProfile, int DirectMethodPointerOffset);

    /// <summary>
    /// Identifies one structural static-field storage mapping by runtime field identity, declaring class identity and exact class-layout evidence.
    /// </summary>
    private readonly record struct FieldStorageCacheKey(nint FieldInfoAddress, nint ClassAddress, string CompatibilityProfile, int StaticFieldsPointerOffset, int StaticFieldsSizeOffset);

    /// <summary>
    /// Identifies one runtime-API static-field storage mapping by runtime field and declaring class identity.
    /// </summary>
    private readonly record struct RuntimeFieldStorageCacheKey(nint FieldInfoAddress, nint ClassAddress);

    /// <summary>Stores validated native method-code mappings.</summary>
    private readonly Dictionary<MethodCodeCacheKey, ResolvedMethodCode> _methodCodes = new();
    /// <summary>Stores validated layout-based static-field storage mappings.</summary>
    private readonly Dictionary<FieldStorageCacheKey, ResolvedFieldStorage> _fieldStorages = new();
    /// <summary>Stores validated runtime-API static-field storage mappings.</summary>
    private readonly Dictionary<RuntimeFieldStorageCacheKey, ResolvedFieldStorage> _runtimeFieldStorages = new();

    /// <summary>Attempts to retrieve a cached assembly resolution.</summary>
    /// <param name="query">The semantic assembly query.</param>
    /// <param name="assembly">Receives the cached result when available.</param>
    /// <returns><see langword="true"/> when a cached result exists.</returns>
    public bool TryGetAssembly(AssemblyQuery query, out ResolvedAssembly? assembly)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _assemblies.TryGetValue(BuildAssemblyKey(query), out assembly);
    }

    /// <summary>Stores a successful assembly resolution.</summary>
    /// <param name="assembly">The resolved assembly to cache.</param>
    public void StoreAssembly(ResolvedAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assemblies[BuildAssemblyKey(assembly.Query)] = assembly;
    }

    /// <summary>Attempts to retrieve a cached type resolution.</summary>
    /// <param name="query">The semantic type query.</param>
    /// <param name="type">Receives the cached result when available.</param>
    /// <returns><see langword="true"/> when a cached result exists.</returns>
    public bool TryGetType(TypeQuery query, out ResolvedType? type)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _types.TryGetValue(BuildTypeKey(query), out type);
    }

    /// <summary>Stores a successful type resolution.</summary>
    /// <param name="type">The resolved type to cache.</param>
    public void StoreType(ResolvedType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        _types[BuildTypeKey(type.Query)] = type;
    }

    /// <summary>Attempts to retrieve a cached method resolution.</summary>
    /// <param name="query">The semantic method query.</param>
    /// <param name="method">Receives the cached result when available.</param>
    /// <returns><see langword="true"/> when a cached result exists.</returns>
    public bool TryGetMethod(MethodQuery query, out ResolvedMethod? method)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _methods.TryGetValue(BuildMethodKey(query), out method);
    }

    /// <summary>Stores a successful method resolution.</summary>
    /// <param name="method">The resolved method to cache.</param>
    public void StoreMethod(ResolvedMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        _methods[BuildMethodKey(method.Query)] = method;
    }

    /// <summary>Attempts to retrieve native method code produced with the exact requested layout.</summary>
    /// <param name="methodInfoAddress">The native <c>MethodInfo*</c> identity.</param>
    /// <param name="layout">The exact method-info layout requested.</param>
    /// <param name="methodCode">Receives the cached code mapping when available.</param>
    /// <returns><see langword="true"/> when a compatible mapping exists.</returns>
    public bool TryGetMethodCode(nint methodInfoAddress, Il2CppMethodInfoLayout layout, out ResolvedMethodCode? methodCode)
    {
        if (methodInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodInfoAddress), "The IL2CPP MethodInfo address cannot be zero.");

        ArgumentNullException.ThrowIfNull(layout);
        MethodCodeCacheKey key = new(methodInfoAddress, layout.Name, layout.DirectMethodPointerOffset);
        return _methodCodes.TryGetValue(key, out methodCode);
    }

    /// <summary>Stores native method code under the exact structural layout that produced it.</summary>
    /// <param name="methodCode">The validated native method-code mapping.</param>
    /// <param name="layout">The structural method layout used to produce the mapping.</param>
    public void StoreMethodCode(ResolvedMethodCode methodCode, Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(methodCode);
        ArgumentNullException.ThrowIfNull(layout);
        MethodCodeCacheKey key = new(methodCode.Method.MethodInfoAddress, layout.Name, layout.DirectMethodPointerOffset);
        _methodCodes[key] = methodCode;
    }

    /// <summary>Attempts to retrieve a cached field resolution.</summary>
    /// <param name="query">The semantic field query.</param>
    /// <param name="field">Receives the cached result when available.</param>
    /// <returns><see langword="true"/> when a cached result exists.</returns>
    public bool TryGetField(FieldQuery query, out ResolvedField? field)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _fields.TryGetValue(BuildFieldKey(query), out field);
    }

    /// <summary>Stores a successful field resolution.</summary>
    /// <param name="field">The resolved field to cache.</param>
    public void StoreField(ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _fields[BuildFieldKey(field.Query)] = field;
    }

    /// <summary>Attempts to retrieve layout-based field storage produced with the exact requested class layout.</summary>
    /// <param name="field">The resolved static field.</param>
    /// <param name="layout">The exact class layout requested.</param>
    /// <param name="storage">Receives the cached storage mapping when available.</param>
    /// <returns><see langword="true"/> when a compatible mapping exists.</returns>
    public bool TryGetFieldStorage(ResolvedField field, Il2CppClassLayout layout, out ResolvedFieldStorage? storage)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(layout);
        FieldStorageCacheKey key = BuildFieldStorageKey(field, layout);
        return _fieldStorages.TryGetValue(key, out storage);
    }

    /// <summary>Stores layout-based field storage under the exact structural class layout that produced it.</summary>
    /// <param name="storage">The validated storage mapping.</param>
    /// <param name="layout">The exact class layout used to produce the mapping.</param>
    public void StoreFieldStorage(ResolvedFieldStorage storage, Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(layout);
        FieldStorageCacheKey key = BuildFieldStorageKey(storage.Field, layout);
        _fieldStorages[key] = storage;
    }

    /// <summary>Attempts to retrieve field storage produced through the optional public IL2CPP runtime API.</summary>
    /// <param name="field">The resolved static field.</param>
    /// <param name="storage">Receives the cached runtime-API mapping when available.</param>
    /// <returns><see langword="true"/> when a compatible mapping exists.</returns>
    public bool TryGetRuntimeFieldStorage(ResolvedField field, out ResolvedFieldStorage? storage)
    {
        ArgumentNullException.ThrowIfNull(field);
        RuntimeFieldStorageCacheKey key = new(field.FieldInfoAddress, field.DeclaringType.ClassAddress);
        return _runtimeFieldStorages.TryGetValue(key, out storage);
    }

    /// <summary>Stores field storage produced through the optional public IL2CPP runtime API.</summary>
    /// <param name="storage">The validated runtime-API storage mapping.</param>
    public void StoreRuntimeFieldStorage(ResolvedFieldStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        RuntimeFieldStorageCacheKey key = new(storage.Field.FieldInfoAddress, storage.Field.DeclaringType.ClassAddress);
        _runtimeFieldStorages[key] = storage;
    }

    /// <summary>Removes every semantic and native result from the current cache generation.</summary>
    public void Clear()
    {
        _assemblies.Clear();
        _types.Clear();
        _methods.Clear();
        _methodCodes.Clear();
        _fields.Clear();
        _fieldStorages.Clear();
        _runtimeFieldStorages.Clear();
    }

    /// <summary>Builds the exact cache identity associated with an assembly query.</summary>
    /// <param name="query">The assembly query to encode.</param>
    /// <returns>A deterministic collision-safe key.</returns>
    private static string BuildAssemblyKey(AssemblyQuery query)
    {
        return BuildCompositeKey(query.Name);
    }

    /// <summary>Builds the exact cache identity associated with a type query.</summary>
    /// <param name="query">The type query to encode.</param>
    /// <returns>A deterministic collision-safe key.</returns>
    private static string BuildTypeKey(TypeQuery query)
    {
        return BuildCompositeKey(query.Assembly.Name, query.Namespace, query.Name);
    }

    /// <summary>Builds the exact cache identity associated with a method query.</summary>
    /// <param name="query">The method query to encode.</param>
    /// <returns>A deterministic collision-safe key including ordered parameter type names.</returns>
    private static string BuildMethodKey(MethodQuery query)
    {
        string[] components = new string[checked(4 + query.ParameterTypeNames.Count)];
        components[0] = query.DeclaringType.Assembly.Name;
        components[1] = query.DeclaringType.Namespace;
        components[2] = query.DeclaringType.Name;
        components[3] = query.Name;

        for (int index = 0; index < query.ParameterTypeNames.Count; index++)
            components[index + 4] = query.ParameterTypeNames[index];

        return BuildCompositeKey(components);
    }

    /// <summary>Builds the exact cache identity associated with a field query.</summary>
    /// <param name="query">The field query to encode.</param>
    /// <returns>A deterministic collision-safe key.</returns>
    private static string BuildFieldKey(FieldQuery query)
    {
        return BuildCompositeKey(query.DeclaringType.Assembly.Name, query.DeclaringType.Namespace, query.DeclaringType.Name, query.Name);
    }

    /// <summary>Builds the strongly typed identity of one layout-based static-field storage mapping.</summary>
    /// <param name="field">The resolved field whose runtime identity should be encoded.</param>
    /// <param name="layout">The exact class layout used to interpret its declaring class.</param>
    /// <returns>The typed cache key.</returns>
    private static FieldStorageCacheKey BuildFieldStorageKey(ResolvedField field, Il2CppClassLayout layout)
    {
        return new FieldStorageCacheKey(field.FieldInfoAddress, field.DeclaringType.ClassAddress, layout.Name, layout.StaticFieldsPointerOffset, layout.StaticFieldsSizeOffset);
    }

    /// <summary>Encodes an ordered sequence of semantic identity components using length-prefixed values.</summary>
    /// <param name="components">The ordered identity components to encode.</param>
    /// <returns>A deterministic collision-safe string.</returns>
    private static string BuildCompositeKey(params string[] components)
    {
        StringBuilder builder = new();

        foreach (string component in components)
        {
            builder.Append(component.Length);
            builder.Append(':');
            builder.Append(component);
        }

        return builder.ToString();
    }
}
