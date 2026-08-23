using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Navigation;

/// <summary>
/// Binds resolved public entities to the internal navigation session without exposing resolution infrastructure through the public API.
/// The binding also owns the monotonically increasing cache generation stamped onto every resolved entity.
/// </summary>
internal sealed class ResolutionBinding : IResolutionNavigator
{
    /// <summary>Stores the session navigator after the resolver stack has completed construction.</summary>
    private IResolutionNavigator? _navigator;

    /// <summary>Stores the current cache generation used to stamp newly resolved runtime entities.</summary>
    private long _generation;

    /// <summary>Gets the current cache generation used by newly materialized resolved entities.</summary>
    public long Generation => _generation;

    /// <summary>Binds this proxy exactly once to the fully constructed session navigator.</summary>
    /// <param name="navigator">The session navigator that will service resolved-entity endpoints.</param>
    /// <exception cref="InvalidOperationException">Thrown when the binding has already been initialized.</exception>
    public void Bind(IResolutionNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);

        if (_navigator is not null)
            throw new InvalidOperationException("The resolution binding is already attached to a navigator.");

        _navigator = navigator;
    }

    /// <summary>Advances the cache generation so previously resolved entities can no longer navigate through stale runtime pointers.</summary>
    public void AdvanceGeneration()
    {
        _generation = checked(_generation + 1);
    }

    /// <summary>Forwards assembly type enumeration to the attached session navigator.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <param name="generation">The cache generation that produced the assembly.</param>
    /// <returns>The resolved types exposed by the assembly image.</returns>
    public IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly, long generation) => GetNavigator().GetTypes(assembly, generation);

    /// <summary>Forwards targeted type resolution relative to an already resolved assembly.</summary>
    /// <param name="assembly">The resolved assembly containing the requested type.</param>
    /// <param name="namespaceName">The exact managed namespace.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <param name="generation">The cache generation that produced the assembly.</param>
    /// <returns>The resolved runtime type.</returns>
    public ResolvedType ResolveType(ResolvedAssembly assembly, string namespaceName, string typeName, long generation) => GetNavigator().ResolveType(assembly, namespaceName, typeName, generation);

    /// <summary>Forwards complete method enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every method declared by the type.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, long generation) => GetNavigator().GetMethods(type, generation);

    /// <summary>Forwards overload enumeration for one exact method name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every matching method overload.</returns>
    public IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name, long generation) => GetNavigator().GetMethods(type, name, generation);

    /// <summary>Forwards targeted exact-overload resolution relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    public ResolvedMethod ResolveMethod(ResolvedType type, string methodName, IReadOnlyList<string> parameterTypeNames, long generation) => GetNavigator().ResolveMethod(type, methodName, parameterTypeNames, generation);

    /// <summary>Forwards complete property enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every property declared by the type.</returns>
    public IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, long generation) => GetNavigator().GetProperties(type, generation);

    /// <summary>Forwards name-filtered property enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed property name.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every matching property.</returns>
    public IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, string name, long generation) => GetNavigator().GetProperties(type, name, generation);

    /// <summary>Forwards targeted property resolution relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="propertyName">The exact managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying the property.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The unique resolved property matching the requested signature.</returns>
    public ResolvedProperty ResolveProperty(ResolvedType type, string propertyName, IReadOnlyList<string> indexParameterTypeNames, long generation) => GetNavigator().ResolveProperty(type, propertyName, indexParameterTypeNames, generation);

    /// <summary>Forwards complete field enumeration for an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>Every field declared by the type.</returns>
    public IReadOnlyList<ResolvedField> GetFields(ResolvedType type, long generation) => GetNavigator().GetFields(type, generation);

    /// <summary>Forwards targeted field resolution relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    public ResolvedField ResolveField(ResolvedType type, string fieldName, long generation) => GetNavigator().ResolveField(type, fieldName, generation);

    /// <summary>Forwards parent-type navigation to the attached session navigator.</summary>
    /// <param name="type">The resolved type whose parent should be retrieved.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The resolved parent type, or <see langword="null"/>.</returns>
    public ResolvedType? GetBaseType(ResolvedType type, long generation) => GetNavigator().GetBaseType(type, generation);

    /// <summary>Forwards interface navigation to the attached session navigator.</summary>
    /// <param name="type">The resolved type whose interfaces should be enumerated.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The resolved interface types.</returns>
    public IReadOnlyList<ResolvedType> GetInterfaces(ResolvedType type, long generation) => GetNavigator().GetInterfaces(type, generation);

    /// <summary>Forwards nested-type navigation to the attached session navigator.</summary>
    /// <param name="type">The resolved type whose nested types should be enumerated.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The resolved nested types.</returns>
    public IReadOnlyList<ResolvedType> GetNestedTypes(ResolvedType type, long generation) => GetNavigator().GetNestedTypes(type, generation);

    /// <summary>Forwards declaring-type navigation to the attached session navigator.</summary>
    /// <param name="type">The resolved type whose declaring type should be retrieved.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The resolved declaring type, or <see langword="null"/>.</returns>
    public ResolvedType? GetDeclaringType(ResolvedType type, long generation) => GetNavigator().GetDeclaringType(type, generation);

    /// <summary>Forwards type metadata inspection to the attached session navigator.</summary>
    /// <param name="type">The resolved type whose metadata should be inspected.</param>
    /// <param name="generation">The cache generation that produced the type.</param>
    /// <returns>The immutable type metadata snapshot.</returns>
    public ResolvedTypeMetadata GetTypeMetadata(ResolvedType type, long generation) => GetNavigator().GetTypeMetadata(type, generation);

    /// <summary>Forwards method metadata inspection to the attached session navigator.</summary>
    /// <param name="method">The resolved method whose metadata should be inspected.</param>
    /// <param name="generation">The cache generation that produced the method.</param>
    /// <returns>The immutable method metadata snapshot.</returns>
    public ResolvedMethodMetadata GetMethodMetadata(ResolvedMethod method, long generation) => GetNavigator().GetMethodMetadata(method, generation);

    /// <summary>Forwards native method-code mapping through the session's active MethodInfo layout policy.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="generation">The cache generation that produced the method.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, long generation) => GetNavigator().ResolveMethodCode(method, generation);

    /// <summary>Forwards native method-code mapping through one explicit MethodInfo layout override.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <param name="generation">The cache generation that produced the method.</param>
    /// <returns>The validated native method-code mapping.</returns>
    public ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout, long generation) => GetNavigator().ResolveMethodCode(method, layout, generation);

    /// <summary>Forwards static-field storage mapping through the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, long generation) => GetNavigator().ResolveFieldStorage(field, generation);

    /// <summary>Forwards static-field storage mapping through one explicit Il2CppClass layout override.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    public ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout, long generation) => GetNavigator().ResolveFieldStorage(field, layout, generation);

    /// <summary>Forwards a scalar normal-static field read through the active storage-selection policy.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type.</typeparam>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated scalar value.</returns>
    public T ReadStaticField<T>(ResolvedField field, long generation) where T : unmanaged => GetNavigator().ReadStaticField<T>(field, generation);

    /// <summary>Forwards a scalar normal-static field read through one explicit class-layout override.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type.</typeparam>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated scalar value.</returns>
    public T ReadStaticField<T>(ResolvedField field, Il2CppClassLayout layout, long generation) where T : unmanaged => GetNavigator().ReadStaticField<T>(field, layout, generation);

    /// <summary>Forwards a managed-reference normal-static field read through the active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static reference field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The remote referenced object address, or zero.</returns>
    public nint ReadStaticFieldReference(ResolvedField field, long generation) => GetNavigator().ReadStaticFieldReference(field, generation);

    /// <summary>Forwards a managed-reference normal-static field read through one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static reference field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The remote referenced object address, or zero.</returns>
    public nint ReadStaticFieldReference(ResolvedField field, Il2CppClassLayout layout, long generation) => GetNavigator().ReadStaticFieldReference(field, layout, generation);

    /// <summary>Forwards an enum normal-static field read through the active storage-selection policy.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="field">The resolved normal static enum field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated enum value.</returns>
    public TEnum ReadStaticFieldEnum<TEnum>(ResolvedField field, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadStaticFieldEnum<TEnum>(field, generation);

    /// <summary>Forwards an enum normal-static field read through one explicit class-layout override.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="field">The resolved normal static enum field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated enum value.</returns>
    public TEnum ReadStaticFieldEnum<TEnum>(ResolvedField field, Il2CppClassLayout layout, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadStaticFieldEnum<TEnum>(field, layout, generation);

    /// <summary>Forwards a scalar instance field read.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type.</typeparam>
    /// <param name="field">The resolved instance field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated scalar value.</returns>
    public T ReadInstanceField<T>(ResolvedField field, nint instanceAddress, long generation) where T : unmanaged => GetNavigator().ReadInstanceField<T>(field, instanceAddress, generation);

    /// <summary>Forwards a managed-reference instance field read.</summary>
    /// <param name="field">The resolved instance reference field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The remote referenced object address, or zero.</returns>
    public nint ReadInstanceFieldReference(ResolvedField field, nint instanceAddress, long generation) => GetNavigator().ReadInstanceFieldReference(field, instanceAddress, generation);

    /// <summary>Forwards an enum instance field read.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="field">The resolved instance enum field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated enum value.</returns>
    public TEnum ReadInstanceFieldEnum<TEnum>(ResolvedField field, nint instanceAddress, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadInstanceFieldEnum<TEnum>(field, instanceAddress, generation);

    /// <summary>Forwards a managed-string normal-static field read through the active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static string field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadStaticFieldString(ResolvedField field, long generation) => GetNavigator().ReadStaticFieldString(field, generation);

    /// <summary>Forwards a managed-string normal-static field read through one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static string field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadStaticFieldString(ResolvedField field, Il2CppClassLayout layout, long generation) => GetNavigator().ReadStaticFieldString(field, layout, generation);

    /// <summary>Forwards a vector-array normal-static field read through the active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static array field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadStaticFieldArray(ResolvedField field, long generation) => GetNavigator().ReadStaticFieldArray(field, generation);

    /// <summary>Forwards a vector-array normal-static field read through one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static array field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadStaticFieldArray(ResolvedField field, Il2CppClassLayout layout, long generation) => GetNavigator().ReadStaticFieldArray(field, layout, generation);

    /// <summary>Forwards a blittable normal-static field read through the active storage-selection policy.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static value-type field.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated raw blittable value.</returns>
    public T ReadStaticFieldBlittable<T>(ResolvedField field, long generation) where T : unmanaged => GetNavigator().ReadStaticFieldBlittable<T>(field, generation);

    /// <summary>Forwards a blittable normal-static field read through one explicit class-layout override.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static value-type field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated raw blittable value.</returns>
    public T ReadStaticFieldBlittable<T>(ResolvedField field, Il2CppClassLayout layout, long generation) where T : unmanaged => GetNavigator().ReadStaticFieldBlittable<T>(field, layout, generation);

    /// <summary>Forwards a managed-string instance field read.</summary>
    /// <param name="field">The resolved instance string field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadInstanceFieldString(ResolvedField field, nint instanceAddress, long generation) => GetNavigator().ReadInstanceFieldString(field, instanceAddress, generation);

    /// <summary>Forwards a vector-array instance field read.</summary>
    /// <param name="field">The resolved instance array field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadInstanceFieldArray(ResolvedField field, nint instanceAddress, long generation) => GetNavigator().ReadInstanceFieldArray(field, instanceAddress, generation);

    /// <summary>Forwards a blittable instance field read.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved instance value-type field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced the field.</param>
    /// <returns>The validated raw blittable value.</returns>
    public T ReadInstanceFieldBlittable<T>(ResolvedField field, nint instanceAddress, long generation) where T : unmanaged => GetNavigator().ReadInstanceFieldBlittable<T>(field, instanceAddress, generation);

    /// <summary>Forwards one scalar array-element read.</summary>
    /// <typeparam name="T">The exact supported scalar element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced the array.</param>
    /// <returns>The validated scalar element.</returns>
    public T ReadArrayElement<T>(ResolvedArray array, int index, long generation) where T : unmanaged => GetNavigator().ReadArrayElement<T>(array, index, generation);

    /// <summary>Forwards one enum array-element read.</summary>
    /// <typeparam name="TEnum">The exact managed enum element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced the array.</param>
    /// <returns>The validated enum element.</returns>
    public TEnum ReadArrayElementEnum<TEnum>(ResolvedArray array, int index, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadArrayElementEnum<TEnum>(array, index, generation);

    /// <summary>Forwards one managed-reference array-element read.</summary>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced the array.</param>
    /// <returns>The remote referenced object address, or zero.</returns>
    public nint ReadArrayElementReference(ResolvedArray array, int index, long generation) => GetNavigator().ReadArrayElementReference(array, index, generation);

    /// <summary>Forwards one managed-string array-element read.</summary>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced the array.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadArrayElementString(ResolvedArray array, int index, long generation) => GetNavigator().ReadArrayElementString(array, index, generation);

    /// <summary>Forwards one blittable array-element read.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced the array.</param>
    /// <returns>The validated raw blittable element.</returns>
    public T ReadArrayElementBlittable<T>(ResolvedArray array, int index, long generation) where T : unmanaged => GetNavigator().ReadArrayElementBlittable<T>(array, index, generation);

    /// <summary>Forwards one instance scalar property getter invocation to the owning session.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated scalar getter result.</returns>
    public T ReadProperty<T>(ResolvedProperty property, nint instanceAddress, long generation) where T : unmanaged => GetNavigator().ReadProperty<T>(property, instanceAddress, generation);

    /// <summary>Forwards one static scalar property getter invocation to the owning session.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated scalar getter result.</returns>
    public T ReadStaticProperty<T>(ResolvedProperty property, long generation) where T : unmanaged => GetNavigator().ReadStaticProperty<T>(property, generation);

    /// <summary>Forwards one instance enum property getter invocation to the owning session.</summary>
    /// <typeparam name="TEnum">The exact managed enum return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated enum getter result.</returns>
    public TEnum ReadPropertyEnum<TEnum>(ResolvedProperty property, nint instanceAddress, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadPropertyEnum<TEnum>(property, instanceAddress, generation);

    /// <summary>Forwards one static enum property getter invocation to the owning session.</summary>
    /// <typeparam name="TEnum">The exact managed enum return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated enum getter result.</returns>
    public TEnum ReadStaticPropertyEnum<TEnum>(ResolvedProperty property, long generation) where TEnum : unmanaged, Enum => GetNavigator().ReadStaticPropertyEnum<TEnum>(property, generation);

    /// <summary>Forwards one instance managed-reference property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The returned managed object pointer, or zero.</returns>
    public nint ReadPropertyReference(ResolvedProperty property, nint instanceAddress, long generation) => GetNavigator().ReadPropertyReference(property, instanceAddress, generation);

    /// <summary>Forwards one static managed-reference property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The returned managed object pointer, or zero.</returns>
    public nint ReadStaticPropertyReference(ResolvedProperty property, long generation) => GetNavigator().ReadStaticPropertyReference(property, generation);

    /// <summary>Forwards one instance string property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadPropertyString(ResolvedProperty property, nint instanceAddress, long generation) => GetNavigator().ReadPropertyString(property, instanceAddress, generation);

    /// <summary>Forwards one static string property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    public string? ReadStaticPropertyString(ResolvedProperty property, long generation) => GetNavigator().ReadStaticPropertyString(property, generation);

    /// <summary>Forwards one instance array property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadPropertyArray(ResolvedProperty property, nint instanceAddress, long generation) => GetNavigator().ReadPropertyArray(property, instanceAddress, generation);

    /// <summary>Forwards one static array property getter invocation to the owning session.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    public ResolvedArray? ReadStaticPropertyArray(ResolvedProperty property, long generation) => GetNavigator().ReadStaticPropertyArray(property, generation);

    /// <summary>Forwards one instance blittable property getter invocation to the owning session.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated raw blittable getter result.</returns>
    public T ReadPropertyBlittable<T>(ResolvedProperty property, nint instanceAddress, long generation) where T : unmanaged => GetNavigator().ReadPropertyBlittable<T>(property, instanceAddress, generation);

    /// <summary>Forwards one static blittable property getter invocation to the owning session.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced the property.</param>
    /// <returns>The validated raw blittable getter result.</returns>
    public T ReadStaticPropertyBlittable<T>(ResolvedProperty property, long generation) where T : unmanaged => GetNavigator().ReadStaticPropertyBlittable<T>(property, generation);

    /// <summary>Gets the attached navigator or rejects navigation before session construction has completed.</summary>
    /// <returns>The fully initialized session navigator.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no navigator has been attached yet.</exception>
    private IResolutionNavigator GetNavigator()
    {
        return _navigator ?? throw new InvalidOperationException("The resolution binding has not been attached to a session navigator.");
    }
}
