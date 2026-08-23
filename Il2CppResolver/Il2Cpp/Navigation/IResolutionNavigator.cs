using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;

namespace UnityIl2CppResolver.Il2Cpp.Navigation;

/// <summary>
/// Defines the internal navigation contract used by resolved public entities to continue semantic traversal, metadata inspection and validated value reading through the session that created them.
/// Every operation carries the originating cache generation so stale runtime pointers are rejected after session invalidation.
/// </summary>
internal interface IResolutionNavigator
{
    /// <summary>Gets every loaded runtime type exposed by the specified resolved assembly.</summary>
    /// <param name="assembly">The resolved assembly whose image should be enumerated.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>The resolved types exposed by the assembly image.</returns>
    IReadOnlyList<ResolvedType> GetTypes(ResolvedAssembly assembly, long generation);

    /// <summary>Resolves one type relative to an already resolved assembly without enumerating the complete image.</summary>
    /// <param name="assembly">The resolved assembly containing the requested type.</param>
    /// <param name="namespaceName">The exact managed namespace. An empty namespace is valid.</param>
    /// <param name="typeName">The exact managed type name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="assembly"/>.</param>
    /// <returns>The resolved runtime type.</returns>
    ResolvedType ResolveType(ResolvedAssembly assembly, string namespaceName, string typeName, long generation);

    /// <summary>Gets every method declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared methods with complete semantic signatures.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, long generation);

    /// <summary>Gets every overload declared by the specified type with the exact requested method name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed method name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All overloads sharing the requested name.</returns>
    IReadOnlyList<ResolvedMethod> GetMethods(ResolvedType type, string name, long generation);

    /// <summary>Resolves one exact method overload relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="methodName">The exact managed method name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names identifying the overload.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved method matching the requested signature.</returns>
    ResolvedMethod ResolveMethod(ResolvedType type, string methodName, IReadOnlyList<string> parameterTypeNames, long generation);

    /// <summary>Gets every property declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared properties with semantic signatures and optional accessors.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, long generation);

    /// <summary>Gets every property declared by the specified type with the exact requested property name.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="name">The exact managed property name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All matching properties.</returns>
    IReadOnlyList<ResolvedProperty> GetProperties(ResolvedType type, string name, long generation);

    /// <summary>Resolves one exact property relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="propertyName">The exact managed property name.</param>
    /// <param name="indexParameterTypeNames">The ordered semantic index-parameter type names identifying the property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved property matching the requested signature.</returns>
    ResolvedProperty ResolveProperty(ResolvedType type, string propertyName, IReadOnlyList<string> indexParameterTypeNames, long generation);

    /// <summary>Gets every field declared by the specified resolved type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>All declared fields with semantic type and storage metadata.</returns>
    IReadOnlyList<ResolvedField> GetFields(ResolvedType type, long generation);

    /// <summary>Resolves one exact field relative to an already resolved declaring type.</summary>
    /// <param name="type">The resolved declaring type.</param>
    /// <param name="fieldName">The exact managed field name.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The unique resolved field matching the requested name.</returns>
    ResolvedField ResolveField(ResolvedType type, string fieldName, long generation);

    /// <summary>Gets the parent type of the specified resolved type.</summary>
    /// <param name="type">The resolved type whose parent should be retrieved.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The identity-mapped parent type, or <see langword="null"/> for a runtime root type.</returns>
    ResolvedType? GetBaseType(ResolvedType type, long generation);

    /// <summary>Gets every interface reported by IL2CPP for the specified resolved type.</summary>
    /// <param name="type">The resolved type whose interfaces should be enumerated.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The identity-mapped interface types.</returns>
    IReadOnlyList<ResolvedType> GetInterfaces(ResolvedType type, long generation);

    /// <summary>Gets every nested type declared by the specified resolved type.</summary>
    /// <param name="type">The resolved type whose nested types should be enumerated.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The identity-mapped nested types.</returns>
    IReadOnlyList<ResolvedType> GetNestedTypes(ResolvedType type, long generation);

    /// <summary>Gets the declaring type of a nested resolved type.</summary>
    /// <param name="type">The resolved type whose declaring type should be retrieved.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The identity-mapped declaring type, or <see langword="null"/> for a top-level type.</returns>
    ResolvedType? GetDeclaringType(ResolvedType type, long generation);

    /// <summary>Gets the cached public metadata snapshot associated with the specified resolved type.</summary>
    /// <param name="type">The resolved type whose metadata should be inspected.</param>
    /// <param name="generation">The cache generation that produced <paramref name="type"/>.</param>
    /// <returns>The immutable type metadata snapshot.</returns>
    ResolvedTypeMetadata GetTypeMetadata(ResolvedType type, long generation);

    /// <summary>Gets the cached public metadata snapshot associated with the specified resolved method.</summary>
    /// <param name="method">The resolved method whose metadata should be inspected.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The immutable method metadata snapshot.</returns>
    ResolvedMethodMetadata GetMethodMetadata(ResolvedMethod method, long generation);

    /// <summary>Maps an already resolved method to native code using the session's active layout-selection policy.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, long generation);

    /// <summary>Maps an already resolved method to native code using one explicit MethodInfo layout override.</summary>
    /// <param name="method">The resolved method to map.</param>
    /// <param name="layout">The one-shot MethodInfo structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="method"/>.</param>
    /// <returns>The validated native method-code mapping.</returns>
    ResolvedMethodCode ResolveMethodCode(ResolvedMethod method, Il2CppMethodInfoLayout layout, long generation);

    /// <summary>Maps an already resolved normal static field to concrete storage using the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, long generation);

    /// <summary>Maps an already resolved normal static field to concrete storage using one explicit Il2CppClass layout override.</summary>
    /// <param name="field">The resolved field whose storage should be mapped.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated concrete static-field storage mapping.</returns>
    ResolvedFieldStorage ResolveFieldStorage(ResolvedField field, Il2CppClassLayout layout, long generation);

    /// <summary>Reads a supported scalar from a normal static field using the session's active storage-selection policy.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type expected by the field.</typeparam>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated scalar value.</returns>
    T ReadStaticField<T>(ResolvedField field, long generation) where T : unmanaged;

    /// <summary>Reads a supported scalar from a normal static field using one explicit class-layout override.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type expected by the field.</typeparam>
    /// <param name="field">The resolved normal static field.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated scalar value.</returns>
    T ReadStaticField<T>(ResolvedField field, Il2CppClassLayout layout, long generation) where T : unmanaged;

    /// <summary>Reads a managed reference from a normal static field using the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static reference field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero for a null reference.</returns>
    nint ReadStaticFieldReference(ResolvedField field, long generation);

    /// <summary>Reads a managed reference from a normal static field using one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static reference field.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero for a null reference.</returns>
    nint ReadStaticFieldReference(ResolvedField field, Il2CppClassLayout layout, long generation);

    /// <summary>Reads an enum from a normal static field using the session's active storage-selection policy.</summary>
    /// <typeparam name="TEnum">The exact managed enum type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static enum field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated enum value.</returns>
    TEnum ReadStaticFieldEnum<TEnum>(ResolvedField field, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Reads an enum from a normal static field using one explicit class-layout override.</summary>
    /// <typeparam name="TEnum">The exact managed enum type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static enum field.</param>
    /// <param name="layout">The one-shot Il2CppClass structural layout.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated enum value.</returns>
    TEnum ReadStaticFieldEnum<TEnum>(ResolvedField field, Il2CppClassLayout layout, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Reads a supported scalar from an instance field relative to one remote IL2CPP object.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar type expected by the field.</typeparam>
    /// <param name="field">The resolved instance field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated scalar value.</returns>
    T ReadInstanceField<T>(ResolvedField field, nint instanceAddress, long generation) where T : unmanaged;

    /// <summary>Reads a managed reference from an instance field relative to one remote IL2CPP object.</summary>
    /// <param name="field">The resolved instance reference field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The referenced remote <c>Il2CppObject*</c> address, or zero for a null reference.</returns>
    nint ReadInstanceFieldReference(ResolvedField field, nint instanceAddress, long generation);

    /// <summary>Reads an enum from an instance field relative to one remote IL2CPP object.</summary>
    /// <typeparam name="TEnum">The exact managed enum type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved instance enum field.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> address containing the field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated enum value.</returns>
    TEnum ReadInstanceFieldEnum<TEnum>(ResolvedField field, nint instanceAddress, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Reads a managed string from a normal static field using the session's active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static string field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadStaticFieldString(ResolvedField field, long generation);

    /// <summary>Reads a managed string from a normal static field using one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static string field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadStaticFieldString(ResolvedField field, Il2CppClassLayout layout, long generation);

    /// <summary>Reads a single-dimensional zero-based managed array from a normal static field using the active storage-selection policy.</summary>
    /// <param name="field">The resolved normal static array field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    ResolvedArray? ReadStaticFieldArray(ResolvedField field, long generation);

    /// <summary>Reads a single-dimensional zero-based managed array from a normal static field using one explicit class-layout override.</summary>
    /// <param name="field">The resolved normal static array field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    ResolvedArray? ReadStaticFieldArray(ResolvedField field, Il2CppClassLayout layout, long generation);

    /// <summary>Reads one explicitly validated blittable value type from a normal static field using the active storage-selection policy.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static value-type field.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    T ReadStaticFieldBlittable<T>(ResolvedField field, long generation) where T : unmanaged;

    /// <summary>Reads one explicitly validated blittable value type from a normal static field using one explicit class-layout override.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved normal static value-type field.</param>
    /// <param name="layout">The one-shot class layout override.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    T ReadStaticFieldBlittable<T>(ResolvedField field, Il2CppClassLayout layout, long generation) where T : unmanaged;

    /// <summary>Reads a managed string from an instance field relative to one remote IL2CPP object.</summary>
    /// <param name="field">The resolved instance string field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadInstanceFieldString(ResolvedField field, nint instanceAddress, long generation);

    /// <summary>Reads a single-dimensional zero-based managed array from an instance field relative to one remote IL2CPP object.</summary>
    /// <param name="field">The resolved instance array field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/> for a null reference.</returns>
    ResolvedArray? ReadInstanceFieldArray(ResolvedField field, nint instanceAddress, long generation);

    /// <summary>Reads one explicitly validated blittable value type from an instance field relative to one remote IL2CPP object.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP field.</typeparam>
    /// <param name="field">The resolved instance value-type field.</param>
    /// <param name="instanceAddress">The remote object base address.</param>
    /// <param name="generation">The cache generation that produced <paramref name="field"/>.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    T ReadInstanceFieldBlittable<T>(ResolvedField field, nint instanceAddress, long generation) where T : unmanaged;

    /// <summary>Reads one explicitly supported scalar element from a validated array.</summary>
    /// <typeparam name="T">The exact supported scalar element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced <paramref name="array"/>.</param>
    /// <returns>The validated scalar element.</returns>
    T ReadArrayElement<T>(ResolvedArray array, int index, long generation) where T : unmanaged;

    /// <summary>Reads one enum element from a validated array.</summary>
    /// <typeparam name="TEnum">The exact managed enum element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced <paramref name="array"/>.</param>
    /// <returns>The validated enum element.</returns>
    TEnum ReadArrayElementEnum<TEnum>(ResolvedArray array, int index, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Reads one managed-reference element from a validated array.</summary>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced <paramref name="array"/>.</param>
    /// <returns>The remote referenced object address, or zero.</returns>
    nint ReadArrayElementReference(ResolvedArray array, int index, long generation);

    /// <summary>Reads one managed string element from a validated array.</summary>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced <paramref name="array"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadArrayElementString(ResolvedArray array, int index, long generation);

    /// <summary>Reads one explicitly validated blittable value-type element from a validated array.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP element type.</typeparam>
    /// <param name="array">The session-bound array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="generation">The cache generation that produced <paramref name="array"/>.</param>
    /// <returns>The raw blittable element reconstructed from target memory.</returns>
    T ReadArrayElementBlittable<T>(ResolvedArray array, int index, long generation) where T : unmanaged;
    /// <summary>Invokes an instance property getter and reads one explicitly supported scalar result.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated scalar getter result.</returns>
    T ReadProperty<T>(ResolvedProperty property, nint instanceAddress, long generation) where T : unmanaged;

    /// <summary>Invokes a static property getter and reads one explicitly supported scalar result.</summary>
    /// <typeparam name="T">The exact supported unmanaged scalar return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated scalar getter result.</returns>
    T ReadStaticProperty<T>(ResolvedProperty property, long generation) where T : unmanaged;

    /// <summary>Invokes an instance property getter and reads one exact enum result.</summary>
    /// <typeparam name="TEnum">The exact managed enum return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated enum getter result.</returns>
    TEnum ReadPropertyEnum<TEnum>(ResolvedProperty property, nint instanceAddress, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Invokes a static property getter and reads one exact enum result.</summary>
    /// <typeparam name="TEnum">The exact managed enum return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated enum getter result.</returns>
    TEnum ReadStaticPropertyEnum<TEnum>(ResolvedProperty property, long generation) where TEnum : unmanaged, Enum;

    /// <summary>Invokes an instance property getter and returns its managed-reference result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The returned <c>Il2CppObject*</c>, or zero for a null reference.</returns>
    nint ReadPropertyReference(ResolvedProperty property, nint instanceAddress, long generation);

    /// <summary>Invokes a static property getter and returns its managed-reference result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The returned <c>Il2CppObject*</c>, or zero for a null reference.</returns>
    nint ReadStaticPropertyReference(ResolvedProperty property, long generation);

    /// <summary>Invokes an instance property getter and decodes its managed-string result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadPropertyString(ResolvedProperty property, nint instanceAddress, long generation);

    /// <summary>Invokes a static property getter and decodes its managed-string result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The decoded string, an empty string, or <see langword="null"/>.</returns>
    string? ReadStaticPropertyString(ResolvedProperty property, long generation);

    /// <summary>Invokes an instance property getter and materializes its single-dimensional zero-based array result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    ResolvedArray? ReadPropertyArray(ResolvedProperty property, nint instanceAddress, long generation);

    /// <summary>Invokes a static property getter and materializes its single-dimensional zero-based array result.</summary>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The validated session-bound array, or <see langword="null"/>.</returns>
    ResolvedArray? ReadStaticPropertyArray(ResolvedProperty property, long generation);

    /// <summary>Invokes an instance property getter and reads one explicitly validated blittable value-type result.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="instanceAddress">The remote <c>Il2CppObject*</c> supplied to the getter.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The raw blittable getter result reconstructed from the boxed payload.</returns>
    T ReadPropertyBlittable<T>(ResolvedProperty property, nint instanceAddress, long generation) where T : unmanaged;

    /// <summary>Invokes a static property getter and reads one explicitly validated blittable value-type result.</summary>
    /// <typeparam name="T">The unmanaged managed value type matching the IL2CPP return type.</typeparam>
    /// <param name="property">The session-bound readable property.</param>
    /// <param name="generation">The cache generation that produced <paramref name="property"/>.</param>
    /// <returns>The raw blittable getter result reconstructed from the boxed payload.</returns>
    T ReadStaticPropertyBlittable<T>(ResolvedProperty property, long generation) where T : unmanaged;
}
