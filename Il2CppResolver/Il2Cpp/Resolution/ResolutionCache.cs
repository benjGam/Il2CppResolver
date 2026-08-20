using System.Text;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;
using UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

namespace UnityIl2CppResolver.Il2Cpp.Resolution;

/// <summary>
/// Stores target-specific semantic resolution results for the lifetime of a single <see cref="ResolutionSession"/>.
/// This cache prevents repeated IL2CPP runtime traversal for identical assembly, type and method queries while remaining strictly scoped to one attached process.
/// Native method-code mappings are cached separately by their resolved <c>MethodInfo*</c> identity because they depend on target-specific runtime layout validation.
/// </summary>
internal sealed class ResolutionCache
{
    /// <summary>
    /// Stores assembly resolution results indexed by the exact semantic assembly query supplied by the caller.
    /// </summary>
    private readonly Dictionary<string, ResolvedAssembly> _assemblies = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores type resolution results indexed by their exact assembly, namespace and type-name identity.
    /// </summary>
    private readonly Dictionary<string, ResolvedType> _types = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores method resolution results indexed by their exact declaring type, method name and ordered parameter type sequence.
    /// </summary>
    private readonly Dictionary<string, ResolvedMethod> _methods = new(StringComparer.Ordinal);

    /// <summary>
    /// Identifies one native method-code mapping by both its target-specific <c>MethodInfo*</c> identity and the exact structural compatibility profile used to interpret it.
    /// The diagnostic profile name forms part of the identity because <see cref="ResolvedMethodCode"/> preserves that name as resolution evidence.
    /// </summary>
    private readonly record struct MethodCodeCacheKey(nint MethodInfoAddress, string CompatibilityProfile, int DirectMethodPointerOffset);

    /// <summary>
    /// Stores validated native method-code mappings indexed by their runtime method identity and the exact structural layout used to obtain them.
    /// </summary>
    private readonly Dictionary<MethodCodeCacheKey, ResolvedMethodCode> _methodCodes = new();

    /// <summary>
    /// Stores field resolution results indexed by their exact declaring type and field-name identity.
    /// </summary>
    private readonly Dictionary<string, ResolvedField> _fields = new(StringComparer.Ordinal);

/// <summary>
/// Stores validated static-field storage mappings indexed by both the target-specific <c>FieldInfo*</c> identity and the structural <c>Il2CppClass</c> layout used to produce the mapping.
/// </summary>
private readonly Dictionary<string, ResolvedFieldStorage> _fieldStorages = new(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to retrieve a previously resolved assembly for the exact supplied query.
    /// </summary>
    /// <param name="query">The semantic assembly query whose cached result should be located.</param>
    /// <param name="assembly">Receives the cached assembly when one exists.</param>
    /// <returns><see langword="true"/> when a cached result exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetAssembly(AssemblyQuery query, out ResolvedAssembly? assembly)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _assemblies.TryGetValue(BuildAssemblyKey(query), out assembly);
    }

    /// <summary>
    /// Stores a successfully resolved assembly using the exact semantic query contained by the result.
    /// Existing results for the same query are replaced because the newest successful resolution represents the current session state.
    /// </summary>
    /// <param name="assembly">The resolved assembly to cache.</param>
    public void StoreAssembly(ResolvedAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _assemblies[BuildAssemblyKey(assembly.Query)] = assembly;
    }

    /// <summary>
    /// Attempts to retrieve a previously resolved type for the exact supplied query.
    /// </summary>
    /// <param name="query">The semantic type query whose cached result should be located.</param>
    /// <param name="type">Receives the cached type when one exists.</param>
    /// <returns><see langword="true"/> when a cached result exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetType(TypeQuery query, out ResolvedType? type)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _types.TryGetValue(BuildTypeKey(query), out type);
    }

    /// <summary>
    /// Stores a successfully resolved type using the exact semantic query contained by the result.
    /// </summary>
    /// <param name="type">The resolved type to cache.</param>
    public void StoreType(ResolvedType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _types[BuildTypeKey(type.Query)] = type;
    }

    /// <summary>
    /// Attempts to retrieve a previously resolved method for the exact supplied semantic signature.
    /// </summary>
    /// <param name="query">The semantic method query whose cached result should be located.</param>
    /// <param name="method">Receives the cached method when one exists.</param>
    /// <returns><see langword="true"/> when a cached result exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetMethod(MethodQuery query, out ResolvedMethod? method)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _methods.TryGetValue(BuildMethodKey(query), out method);
    }

    /// <summary>
    /// Stores a successfully resolved method using its exact declaring type, method name and ordered parameter signature.
    /// </summary>
    /// <param name="method">The resolved method to cache.</param>
    public void StoreMethod(ResolvedMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _methods[BuildMethodKey(method.Query)] = method;
    }

    /// <summary>
    /// Attempts to retrieve a previously validated native code mapping for the specified runtime method identity.
    /// </summary>
    /// <param name="methodInfoAddress">The target-specific native <c>MethodInfo*</c> address.</param>
    /// <param name="methodCode">Receives the cached native method-code mapping when one exists.</param>
    /// <returns><see langword="true"/> when a cached mapping exists; otherwise <see langword="false"/>.</returns>
    /// <summary>
    /// Attempts to retrieve a previously validated native code mapping for the specified runtime method and structural layout.
    /// </summary>
    /// <param name="methodInfoAddress">The target-specific native <c>MethodInfo*</c> address.</param>
    /// <param name="layout">The exact <c>MethodInfo</c> layout requested by the caller.</param>
    /// <param name="methodCode">Receives the compatible cached native method-code mapping when one exists.</param>
    /// <returns><see langword="true"/> when a compatible cached mapping exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetMethodCode(nint methodInfoAddress, Il2CppMethodInfoLayout layout, out ResolvedMethodCode? methodCode)
    {
        if (methodInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodInfoAddress), "The IL2CPP MethodInfo address cannot be zero.");

        ArgumentNullException.ThrowIfNull(layout);

        MethodCodeCacheKey key = new(methodInfoAddress, layout.Name, layout.DirectMethodPointerOffset);

        return _methodCodes.TryGetValue(key, out methodCode);
    }

    /// <summary>
    /// Stores a validated native method-code mapping using both its target-specific <c>MethodInfo*</c> identity and the exact structural layout that produced it.
    /// </summary>
    /// <param name="methodCode">The validated native method-code mapping to cache.</param>
    /// <param name="layout">The structural layout used to produce the mapping.</param>
    public void StoreMethodCode(ResolvedMethodCode methodCode, Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(methodCode);
        ArgumentNullException.ThrowIfNull(layout);

        MethodCodeCacheKey key = new(methodCode.Method.MethodInfoAddress, layout.Name, layout.DirectMethodPointerOffset);

        _methodCodes[key] = methodCode;
    }

    /// <summary>
    /// Attempts to retrieve a previously resolved field for the exact supplied semantic query.
    /// </summary>
    /// <param name="query">The semantic field query whose cached result should be located.</param>
    /// <param name="field">Receives the cached field when one exists.</param>
    /// <returns><see langword="true"/> when a cached result exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetField(FieldQuery query, out ResolvedField? field)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _fields.TryGetValue(BuildFieldKey(query), out field);
    }

    /// <summary>
    /// Stores a successfully resolved field using its exact declaring type and field name.
    /// </summary>
    /// <param name="field">The resolved field to cache.</param>
    public void StoreField(ResolvedField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        _fields[BuildFieldKey(field.Query)] = field;
    }

    /// <summary>
    /// Attempts to retrieve a previously validated static-field storage mapping produced with the specified class layout.
    /// The structural offsets form part of the cache identity so resolving the same field through different compatibility profiles cannot reuse incompatible storage evidence.
    /// </summary>
    /// <param name="fieldInfoAddress">The target-specific native <c>FieldInfo*</c> address.</param>
    /// <param name="layout">The explicit <c>Il2CppClass</c> layout associated with the requested mapping.</param>
    /// <param name="storage">Receives the cached storage mapping when one exists.</param>
    /// <returns><see langword="true"/> when a compatible cached mapping exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetFieldStorage(nint fieldInfoAddress, Il2CppClassLayout layout, out ResolvedFieldStorage? storage)
    {
        if (fieldInfoAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldInfoAddress), "The IL2CPP FieldInfo address cannot be zero.");

        ArgumentNullException.ThrowIfNull(layout);

        string key = BuildFieldStorageKey(fieldInfoAddress, layout);

        return _fieldStorages.TryGetValue(key, out storage);
    }

    /// <summary>
    /// Stores a validated static-field storage mapping using both its target-specific <c>FieldInfo*</c> identity and the structural layout that produced it.
    /// </summary>
    /// <param name="storage">The validated field storage mapping to cache.</param>
    /// <param name="layout">The explicit class layout used to produce the mapping.</param>
    public void StoreFieldStorage(ResolvedFieldStorage storage, Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(layout);

        string key = BuildFieldStorageKey(storage.Field.FieldInfoAddress, layout);

        _fieldStorages[key] = storage;
    }

    /// <summary>
    /// Builds the exact cache identity associated with a static-field storage mapping.
    /// The layout name is intentionally excluded because structural offsets, rather than diagnostic labels, define compatibility.
    /// </summary>
    /// <param name="fieldInfoAddress">The target-specific native <c>FieldInfo*</c> address.</param>
    /// <param name="layout">The class layout whose structural offsets form part of the mapping identity.</param>
    /// <returns>A collision-safe cache key identifying one field under one exact structural layout.</returns>
    private static string BuildFieldStorageKey(nint fieldInfoAddress, Il2CppClassLayout layout)
    {
        return BuildCompositeKey(
            fieldInfoAddress.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
            layout.StaticFieldsPointerOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            layout.StaticFieldsSizeOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Builds the exact cache identity associated with a field query.
    /// </summary>
    /// <param name="query">The semantic field query to encode.</param>
    /// <returns>A collision-safe cache key preserving the complete requested field identity.</returns>
    private static string BuildFieldKey(FieldQuery query)
    {
        return BuildCompositeKey(query.DeclaringType.Assembly.Name, query.DeclaringType.Namespace, query.DeclaringType.Name, query.Name);
    }

    /// <summary>
    /// Removes every semantic and native resolution result currently associated with the session.
    /// Subsequent resolver operations will interrogate the target runtime again and repopulate the cache from fresh evidence.
    /// </summary>
    public void Clear()
    {
        _assemblies.Clear();
        _types.Clear();
        _methods.Clear();
        _methodCodes.Clear();
        _fields.Clear();
        _fieldStorages.Clear();
    }

    /// <summary>
    /// Builds the exact cache identity associated with an assembly query.
    /// </summary>
    /// <param name="query">The semantic assembly query to encode.</param>
    /// <returns>A collision-safe cache key preserving the exact supplied assembly identity.</returns>
    private static string BuildAssemblyKey(AssemblyQuery query)
    {
        return BuildCompositeKey(query.Name);
    }

    /// <summary>
    /// Builds the exact cache identity associated with a type query.
    /// </summary>
    /// <param name="query">The semantic type query to encode.</param>
    /// <returns>A collision-safe cache key preserving assembly, namespace and type identity.</returns>
    private static string BuildTypeKey(TypeQuery query)
    {
        return BuildCompositeKey(query.Assembly.Name, query.Namespace, query.Name);
    }

    /// <summary>
    /// Builds the exact cache identity associated with a method query.
    /// The ordered parameter sequence forms part of the key so overloaded methods remain independent cache entries.
    /// </summary>
    /// <param name="query">The semantic method query to encode.</param>
    /// <returns>A collision-safe cache key preserving the complete requested method signature.</returns>
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

    /// <summary>
    /// Encodes an ordered sequence of semantic identity components using length-prefixed values.
    /// Length-prefix encoding prevents collisions without depending on reserved separator characters that could theoretically appear in metadata identifiers.
    /// </summary>
    /// <param name="components">The ordered identity components to encode.</param>
    /// <returns>A deterministic collision-safe string suitable for dictionary indexing.</returns>
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