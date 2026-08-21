using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Executes end-to-end integration checks against a live IL2CPP target.
/// The test validates semantic resolution, navigation endpoints, identity-map reuse, native method mapping, property resolution, static-field storage mapping, safe field-value reading, type metadata, session caching and generation invalidation.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes the live resolver integration test against the first running Dofus process.
    /// </summary>
    /// <param name="args">Command-line arguments are currently ignored by this development harness.</param>
    /// <returns>Zero when every validation succeeds; otherwise a non-zero exit code.</returns>
    public static int Main(string[] args)
    {

        int processId = Process.GetProcessesByName("Dofus")[0].Id;

        try
        {
            Run(processId);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("ALL RESOLVER TESTS PASSED");
            Console.WriteLine("========================================");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("TEST FAILURE");
            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine(exception);

            return 2;
        }
    }

    /// <summary>
    /// Executes every resolver and navigation validation against the specified target process.
    /// </summary>
    /// <param name="processId">The Windows process identifier of the live IL2CPP target.</param>
    private static void Run(int processId)
    {
        using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

        resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
        resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

        Console.WriteLine($"Attached to PID {resolver.ProcessId}.");
        Console.WriteLine();

        ResolvedAssembly assembly = TestAssemblyResolution(resolver);
        ResolvedType type = TestTypeNavigation(resolver, assembly);
        ResolvedMethod method = TestMethodNavigation(resolver, type);
        TestMethodCodeNavigation(resolver, method);
        ResolvedProperty property = TestPropertyNavigation(resolver, type);
        ResolvedField field = TestFieldNavigation(resolver, type);
        TestFieldStorageNavigation(resolver, field);
        TestTypeRelationships(resolver, assembly, type);
        TestMetadata(assembly, type, method);
        ResolvedArray? array = TestFieldValueReading(assembly, field);
        TestPropertyValueReading(assembly, type);
        TestIdentityMap(resolver, assembly, type, method, property, field);
        TestRepeatedNavigation(assembly, type);
        TestCacheInvalidation(resolver, assembly, type, method, property, field, array);
    }

    /// <summary>
    /// Validates assembly enumeration and targeted assembly resolution.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <returns>The resolved Unity Input System assembly.</returns>
    private static ResolvedAssembly TestAssemblyResolution(Il2CppResolver resolver)
    {
        Section("Assembly navigation");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedAssembly> assemblies = resolver.GetAssemblies();
        stopwatch.Stop();

        Require(assemblies.Count > 0, "GetAssemblies returned no assemblies.");

        Console.WriteLine($"Assemblies: {assemblies.Count}");
        Console.WriteLine($"First enumeration: {stopwatch.ElapsedMilliseconds} ms");

        ResolvedAssembly? enumeratedAssembly = assemblies.FirstOrDefault(
            assembly => string.Equals(assembly.Name, "Unity.InputSystem.dll", StringComparison.OrdinalIgnoreCase));

        Require(enumeratedAssembly is not null, "Unity.InputSystem.dll was not returned by GetAssemblies.");

        ResolvedAssembly resolvedAssembly = resolver.ResolveAssembly(
            new AssemblyQuery("Unity.InputSystem"));

        Require(resolvedAssembly.AssemblyAddress != 0, "Resolved assembly has a null Assembly*.");
        Require(resolvedAssembly.ImageAddress != 0, "Resolved assembly has a null Image*.");
        Require(ReferenceEquals(enumeratedAssembly, resolvedAssembly), "Assembly identity map did not reuse the same ResolvedAssembly instance.");

        Console.WriteLine($"Assembly:   {resolvedAssembly.Name}");
        Console.WriteLine($"Assembly*:  0x{resolvedAssembly.AssemblyAddress:X}");
        Console.WriteLine($"Image*:     0x{resolvedAssembly.ImageAddress:X}");
        Console.WriteLine("Identity:   OK");

        Pass();

        return resolvedAssembly;
    }

    /// <summary>
    /// Validates type enumeration, targeted type resolution and type identity reuse.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="assembly">The resolved Unity Input System assembly.</param>
    /// <returns>The resolved <c>UnityEngine.InputSystem.InputSystem</c> type.</returns>
    private static ResolvedType TestTypeNavigation(Il2CppResolver resolver, ResolvedAssembly assembly)
    {
        Section("Type navigation");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedType> types = assembly.GetTypes();
        stopwatch.Stop();

        Require(types.Count > 0, "GetTypes returned no types.");

        Console.WriteLine($"Types: {types.Count}");
        Console.WriteLine($"First enumeration: {stopwatch.ElapsedMilliseconds} ms");

        ResolvedType? enumeratedType = types.FirstOrDefault(
            type =>
                string.Equals(type.Query.Namespace, "UnityEngine.InputSystem", StringComparison.Ordinal) &&
                string.Equals(type.Query.Name, "InputSystem", StringComparison.Ordinal));

        Require(enumeratedType is not null, "InputSystem was not returned by assembly.GetTypes().");

        ResolvedType navigationType = assembly.ResolveType(
            "UnityEngine.InputSystem",
            "InputSystem");

        ResolvedType directType = resolver.ResolveType(
            new TypeQuery(
                "Unity.InputSystem",
                "UnityEngine.InputSystem",
                "InputSystem"));

        Require(navigationType.ClassAddress != 0, "Resolved type has a null Il2CppClass*.");
        Require(ReferenceEquals(enumeratedType, navigationType), "Type enumeration and assembly.ResolveType did not return the same object.");
        Require(ReferenceEquals(navigationType, directType), "Assembly navigation and resolver.ResolveType did not share the identity map.");

        Console.WriteLine($"Type:        {navigationType.Query.Namespace}.{navigationType.Query.Name}");
        Console.WriteLine($"Il2CppClass: 0x{navigationType.ClassAddress:X}");
        Console.WriteLine("Identity:    OK");

        Pass();

        return navigationType;
    }

    /// <summary>
    /// Validates method enumeration, name-filtered navigation, exact overload resolution and method identity reuse.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="type">The resolved InputSystem type.</param>
    /// <returns>The resolved QueueEvent overload used by the native mapping test.</returns>
    private static ResolvedMethod TestMethodNavigation(Il2CppResolver resolver, ResolvedType type)
    {
        Section("Method navigation");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedMethod> queueEventOverloads = type.GetMethods("QueueEvent");
        stopwatch.Stop();

        Require(queueEventOverloads.Count > 0, "GetMethods(\"QueueEvent\") returned no overloads.");

        Console.WriteLine($"QueueEvent overloads: {queueEventOverloads.Count}");
        Console.WriteLine($"Filtered enumeration: {stopwatch.ElapsedMilliseconds} ms");

        foreach (ResolvedMethod candidate in queueEventOverloads)
            Console.WriteLine($"  {FormatMethod(candidate)}");

        ResolvedMethod navigationMethod = type.ResolveMethod(
            "QueueEvent",
            "UnityEngine.InputSystem.LowLevel.InputEventPtr");

        ResolvedMethod directMethod = resolver.ResolveMethod(
            new MethodQuery(
                "Unity.InputSystem",
                "UnityEngine.InputSystem",
                "InputSystem",
                "QueueEvent",
                "UnityEngine.InputSystem.LowLevel.InputEventPtr"));

        ResolvedMethod? enumeratedMethod = queueEventOverloads.FirstOrDefault(
            method => method.MethodInfoAddress == navigationMethod.MethodInfoAddress);

        Require(enumeratedMethod is not null, "Resolved QueueEvent overload was not present in GetMethods(\"QueueEvent\").");
        Require(ReferenceEquals(enumeratedMethod, navigationMethod), "Filtered method navigation did not reuse the identity-mapped method.");
        Require(ReferenceEquals(navigationMethod, directMethod), "Type.ResolveMethod and resolver.ResolveMethod did not return the same object.");
        Require(navigationMethod.MethodInfoAddress != 0, "Resolved method has a null MethodInfo*.");
        Require(string.Equals(navigationMethod.ReturnTypeName, "System.Void", StringComparison.Ordinal), "QueueEvent return type is not System.Void.");
        Require(navigationMethod.ParameterTypeNames.Count == 1, "QueueEvent parameter count is not one.");
        Require(string.Equals(navigationMethod.ParameterTypeNames[0], "UnityEngine.InputSystem.LowLevel.InputEventPtr", StringComparison.Ordinal), "QueueEvent parameter type is incorrect.");

        Console.WriteLine($"Selected:    {FormatMethod(navigationMethod)}");
        Console.WriteLine($"MethodInfo*: 0x{navigationMethod.MethodInfoAddress:X}");
        Console.WriteLine("Identity:    OK");

        Console.WriteLine();
        Console.WriteLine("Enumerating all methods...");

        stopwatch.Restart();
        IReadOnlyList<ResolvedMethod> allMethods = type.GetMethods();
        stopwatch.Stop();

        Require(allMethods.Count >= queueEventOverloads.Count, "Complete method enumeration returned fewer entries than the filtered enumeration.");
        Require(allMethods.Any(method => ReferenceEquals(method, navigationMethod)), "Complete method enumeration did not reuse the resolved QueueEvent object.");

        Console.WriteLine($"Methods:          {allMethods.Count}");
        Console.WriteLine($"Full enumeration: {stopwatch.ElapsedMilliseconds} ms");

        Pass();

        return navigationMethod;
    }

    /// <summary>
    /// Validates native method-code resolution through both configured and one-shot layout paths.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="method">The resolved QueueEvent method.</param>
    private static void TestMethodCodeNavigation(Il2CppResolver resolver, ResolvedMethod method)
    {
        Section("Method code navigation");

        ResolvedMethodCode configuredCode = method.ResolveCode();

        Require(configuredCode.NativeAddress != 0, "Configured method code resolution returned a null native address.");
        Require(ReferenceEquals(configuredCode.Method, method), "ResolvedMethodCode does not reference the originating ResolvedMethod.");

        Console.WriteLine($"MethodInfo*: 0x{method.MethodInfoAddress:X}");
        Console.WriteLine($"Native:      0x{configuredCode.NativeAddress:X}");
        Console.WriteLine($"Section:     {configuredCode.SectionName}");
        Console.WriteLine($"Profile:     {configuredCode.CompatibilityProfile}");

        ResolvedMethodCode overrideCode = method.ResolveCode(
            Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

        Require(overrideCode.NativeAddress == configuredCode.NativeAddress, "Configured and one-shot method layouts produced different native addresses.");

        ResolvedMethodCode resolverCode = resolver.ResolveMethodCode(method.Query);

        Require(resolverCode.NativeAddress == configuredCode.NativeAddress, "Navigation and resolver-level method code resolution disagree.");

        Console.WriteLine("Configured layout: OK");
        Console.WriteLine("One-shot override: OK");
        Console.WriteLine("Resolver parity:   OK");

        Pass();
    }

    /// <summary>
    /// Validates property enumeration, name-filtered navigation, exact indexed-property resolution and accessor identity reuse.
    /// The test selects one property dynamically from the live InputSystem type so it does not depend on a hard-coded property name.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="type">The resolved InputSystem type.</param>
    /// <returns>The resolved property selected for identity-map and generation-invalidation tests.</returns>
    private static ResolvedProperty TestPropertyNavigation(Il2CppResolver resolver, ResolvedType type)
    {
        Section("Property navigation");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedProperty> properties = type.GetProperties();
        stopwatch.Stop();

        Require(properties.Count > 0, "GetProperties returned no properties.");

        Console.WriteLine($"Properties: {properties.Count}");
        Console.WriteLine($"First enumeration: {stopwatch.ElapsedMilliseconds} ms");

        foreach (ResolvedProperty candidate in properties.Take(Math.Min(properties.Count, 10)))
            Console.WriteLine($"  {FormatProperty(candidate)}");

        ResolvedProperty selected = properties.FirstOrDefault(property => property.CanRead && property.CanWrite)
            ?? properties.FirstOrDefault(property => property.CanRead)
            ?? properties.FirstOrDefault(property => property.CanWrite)
            ?? properties[0];

        IReadOnlyList<ResolvedProperty> namedProperties = type.GetProperties(selected.Query.Name);
        Require(namedProperties.Count > 0, $"GetProperties(\"{selected.Query.Name}\") returned no properties.");

        ResolvedProperty? filteredProperty = namedProperties.FirstOrDefault(property => property.PropertyInfoAddress == selected.PropertyInfoAddress);
        Require(filteredProperty is not null, "The selected property was not present in name-filtered property navigation.");
        Require(ReferenceEquals(filteredProperty, selected), "Name-filtered property navigation did not reuse the identity-mapped property object.");

        ResolvedProperty navigationProperty = type.ResolveProperty(selected.Query.Name, selected.IndexParameterTypeNames.ToArray());
        ResolvedProperty directProperty = resolver.ResolveProperty(new PropertyQuery(type.Query, selected.Query.Name, selected.IndexParameterTypeNames.ToArray()));

        Require(ReferenceEquals(selected, navigationProperty), "Property enumeration and type.ResolveProperty did not return the same object.");
        Require(ReferenceEquals(navigationProperty, directProperty), "Type.ResolveProperty and resolver.ResolveProperty did not share the identity map.");
        Require(navigationProperty.PropertyInfoAddress != 0, "Resolved property has a null PropertyInfo*.");
        Require(!string.IsNullOrWhiteSpace(navigationProperty.TypeName), "Resolved property has an empty semantic type name.");
        Require(navigationProperty.CanRead || navigationProperty.CanWrite, "Resolved property exposes neither a getter nor a setter.");

        if (navigationProperty.Getter is not null)
        {
            ResolvedMethod getterAgain = type.ResolveMethod(navigationProperty.Getter.Query.Name, navigationProperty.Getter.ParameterTypeNames.ToArray());
            Require(ReferenceEquals(navigationProperty.Getter, getterAgain), "Property getter did not reuse the MethodInfo identity map.");
            Require(string.Equals(navigationProperty.Getter.ReturnTypeName, navigationProperty.TypeName, StringComparison.Ordinal), "Property getter return type does not match the resolved property type.");
            Require(ParametersEqual(navigationProperty.Getter.ParameterTypeNames, navigationProperty.IndexParameterTypeNames), "Property getter parameters do not match the resolved index signature.");
        }

        if (navigationProperty.Setter is not null)
        {
            ResolvedMethod setterAgain = type.ResolveMethod(navigationProperty.Setter.Query.Name, navigationProperty.Setter.ParameterTypeNames.ToArray());
            Require(ReferenceEquals(navigationProperty.Setter, setterAgain), "Property setter did not reuse the MethodInfo identity map.");
            Require(string.Equals(navigationProperty.Setter.ReturnTypeName, "System.Void", StringComparison.Ordinal), "Property setter does not return System.Void.");
            Require(navigationProperty.Setter.ParameterTypeNames.Count == navigationProperty.IndexParameterTypeNames.Count + 1, "Property setter parameter count does not match the index signature plus value parameter.");

            for (int index = 0; index < navigationProperty.IndexParameterTypeNames.Count; index++)
                Require(string.Equals(navigationProperty.Setter.ParameterTypeNames[index], navigationProperty.IndexParameterTypeNames[index], StringComparison.Ordinal), $"Property setter index parameter mismatch at index {index}.");

            Require(string.Equals(navigationProperty.Setter.ParameterTypeNames[^1], navigationProperty.TypeName, StringComparison.Ordinal), "Property setter value parameter does not match the resolved property type.");
        }

        Console.WriteLine($"Selected:      {FormatProperty(navigationProperty)}");
        Console.WriteLine($"PropertyInfo*: 0x{navigationProperty.PropertyInfoAddress:X}");
        Console.WriteLine($"CanRead:       {navigationProperty.CanRead}");
        Console.WriteLine($"CanWrite:      {navigationProperty.CanWrite}");
        Console.WriteLine($"Getter:        {(navigationProperty.Getter is null ? "<none>" : FormatMethod(navigationProperty.Getter))}");
        Console.WriteLine($"Setter:        {(navigationProperty.Setter is null ? "<none>" : FormatMethod(navigationProperty.Setter))}");
        Console.WriteLine("Identity:      OK");

        Pass();

        return navigationProperty;
    }

    /// <summary>
    /// Validates field enumeration, exact field resolution and field identity reuse.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="type">The resolved InputSystem type.</param>
    /// <returns>The resolved <c>s_Manager</c> static field.</returns>
    private static ResolvedField TestFieldNavigation(Il2CppResolver resolver, ResolvedType type)
    {
        Section("Field navigation");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedField> fields = type.GetFields();
        stopwatch.Stop();

        Require(fields.Count > 0, "GetFields returned no fields.");

        Console.WriteLine($"Fields: {fields.Count}");
        Console.WriteLine($"First enumeration: {stopwatch.ElapsedMilliseconds} ms");

        ResolvedField? enumeratedField = fields.FirstOrDefault(
            field => string.Equals(field.Query.Name, "s_Manager", StringComparison.Ordinal));

        Require(enumeratedField is not null, "s_Manager was not returned by GetFields().");

        ResolvedField navigationField = type.ResolveField("s_Manager");

        ResolvedField directField = resolver.ResolveField(
            new FieldQuery(
                "Unity.InputSystem",
                "UnityEngine.InputSystem",
                "InputSystem",
                "s_Manager"));

        Require(ReferenceEquals(enumeratedField, navigationField), "Field enumeration and type.ResolveField did not return the same object.");
        Require(ReferenceEquals(navigationField, directField), "Type.ResolveField and resolver.ResolveField did not share the identity map.");
        Require(navigationField.FieldInfoAddress != 0, "Resolved field has a null FieldInfo*.");
        Require(navigationField.StorageKind == FieldStorageKind.Static, "s_Manager is not classified as normal static storage.");
        Require(navigationField.StaticStorageOffset == 0x8, $"s_Manager static offset is 0x{navigationField.StaticStorageOffset:X}, expected 0x8.");

        Console.WriteLine($"Field:       {navigationField.Query.Name}");
        Console.WriteLine($"FieldInfo*:  0x{navigationField.FieldInfoAddress:X}");
        Console.WriteLine($"Type:        {navigationField.TypeName}");
        Console.WriteLine($"Storage:     {navigationField.StorageKind}");
        Console.WriteLine($"Offset:      0x{navigationField.StaticStorageOffset:X}");
        Console.WriteLine("Identity:    OK");

        Pass();

        return navigationField;
    }

    /// <summary>
    /// Validates static-field storage mapping through configured and one-shot class layouts.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="field">The resolved <c>s_Manager</c> field.</param>
    private static void TestFieldStorageNavigation(Il2CppResolver resolver, ResolvedField field)
    {
        Section("Field storage navigation");

        ResolvedFieldStorage configuredStorage = field.ResolveStorage();

        Require(configuredStorage.StaticFieldsAddress != 0, "Resolved static-fields base address is null.");
        Require(configuredStorage.StorageAddress != 0, "Resolved field storage address is null.");
        Require(configuredStorage.StaticFieldsSize > configuredStorage.StaticStorageOffset, "Static field offset lies outside the static-fields block.");

        nint expectedAddress = checked(configuredStorage.StaticFieldsAddress + (int)configuredStorage.StaticStorageOffset);

        Require(configuredStorage.StorageAddress == expectedAddress, "StorageAddress does not equal static_fields + StaticStorageOffset.");
        Require(configuredStorage.StaticStorageOffset == 0x8, "Resolved s_Manager storage offset changed unexpectedly.");

        Console.WriteLine($"Static fields:   0x{configuredStorage.StaticFieldsAddress:X}");
        Console.WriteLine($"Static size:     0x{configuredStorage.StaticFieldsSize:X}");
        Console.WriteLine($"Field offset:    0x{configuredStorage.StaticStorageOffset:X}");
        Console.WriteLine($"Storage address: 0x{configuredStorage.StorageAddress:X}");
        Console.WriteLine($"Profile:         {configuredStorage.CompatibilityProfile}");

        ResolvedFieldStorage overrideStorage = field.ResolveStorage(
            Il2CppClassLayouts.Class29_2X64);

        Require(overrideStorage.StorageAddress == configuredStorage.StorageAddress, "Configured and one-shot class layouts produced different storage addresses.");

        ResolvedFieldStorage resolverStorage = resolver.ResolveFieldStorage(field.Query);

        Require(resolverStorage.StorageAddress == configuredStorage.StorageAddress, "Navigation and resolver-level field storage resolution disagree.");

        Console.WriteLine("Configured layout: OK");
        Console.WriteLine("One-shot override: OK");
        Console.WriteLine("Resolver parity:   OK");

        Pass();
    }

    /// <summary>
    /// Validates parent, interface, nested-type and declaring-type navigation together with resolved-type identity reuse.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="assembly">The resolved Unity Input System assembly.</param>
    /// <param name="type">The resolved top-level InputSystem type.</param>
    private static void TestTypeRelationships(Il2CppResolver resolver, ResolvedAssembly assembly, ResolvedType type)
    {
        Section("Type relationships");

        ResolvedType? baseType = type.GetBaseType();
        Require(baseType is not null, "InputSystem did not expose a base type.");

        ResolvedType baseTypeAgain = resolver.ResolveType(baseType.Query);
        Require(ReferenceEquals(baseType, baseTypeAgain), "Base-type navigation did not reuse the resolved-type identity map.");

        IReadOnlyList<ResolvedType> interfaces = type.GetInterfaces();
        IReadOnlyList<ResolvedType> nestedTypes = type.GetNestedTypes();
        ResolvedType? declaringType = type.GetDeclaringType();

        Require(declaringType is null, "Top-level InputSystem unexpectedly reports a declaring type.");

        foreach (ResolvedType interfaceType in interfaces)
        {
            ResolvedType interfaceAgain = resolver.ResolveType(interfaceType.Query);
            Require(ReferenceEquals(interfaceType, interfaceAgain), $"Interface '{interfaceType.Query.Name}' did not reuse the resolved-type identity map.");
        }

        foreach (ResolvedType nestedType in nestedTypes)
        {
            ResolvedType? nestedDeclaringType = nestedType.GetDeclaringType();
            Require(nestedDeclaringType is not null, $"Nested type '{nestedType.Query.Name}' does not expose a declaring type.");
            Require(ReferenceEquals(type, nestedDeclaringType), $"Nested type '{nestedType.Query.Name}' did not resolve back to InputSystem as its declaring type.");
        }

        Console.WriteLine($"Base type:    {baseType.Query.Namespace}.{baseType.Query.Name}");
        Console.WriteLine($"Interfaces:   {interfaces.Count}");
        Console.WriteLine($"Nested types: {nestedTypes.Count}");
        Console.WriteLine("Declaring:    <none>");
        Console.WriteLine("Identity:     OK");

        if (interfaces.Count > 0)
            Console.WriteLine($"First interface: {interfaces[0].Query.Namespace}.{interfaces[0].Query.Name}");

        if (nestedTypes.Count > 0)
            Console.WriteLine($"First nested:    {nestedTypes[0].Query.Namespace}.{nestedTypes[0].Query.Name}");

        Pass();
    }

    /// <summary>
    /// Validates lazy type and method metadata inspection, cached snapshot reuse and value-type/enum classification on known Input System types.
    /// </summary>
    /// <param name="assembly">The resolved Unity Input System assembly.</param>
    /// <param name="type">The resolved InputSystem type.</param>
    /// <param name="method">The resolved QueueEvent method.</param>
    private static void TestMetadata(ResolvedAssembly assembly, ResolvedType type, ResolvedMethod method)
    {
        Section("Type and method metadata");

        ResolvedTypeMetadata typeMetadata = type.GetMetadata();
        ResolvedTypeMetadata typeMetadataAgain = type.GetMetadata();
        ResolvedMethodMetadata methodMetadata = method.GetMetadata();
        ResolvedMethodMetadata methodMetadataAgain = method.GetMetadata();

        Require(ReferenceEquals(typeMetadata, typeMetadataAgain), "Repeated type metadata inspection did not reuse the cached public snapshot.");
        Require(ReferenceEquals(methodMetadata, methodMetadataAgain), "Repeated method metadata inspection did not reuse the cached public snapshot.");
        Require(methodMetadata.IsStatic, "InputSystem.QueueEvent is expected to be static.");

        ResolvedType inputEventPtr = assembly.ResolveType("UnityEngine.InputSystem.LowLevel", "InputEventPtr");
        ResolvedTypeMetadata inputEventPtrMetadata = inputEventPtr.GetMetadata();
        Require(inputEventPtrMetadata.IsValueType, "InputEventPtr is expected to be a value type.");
        Require(inputEventPtrMetadata.ValueSize is > 0, "InputEventPtr value metadata did not expose a positive value size.");

        ResolvedType inputActionPhase = assembly.ResolveType("UnityEngine.InputSystem", "InputActionPhase");
        ResolvedTypeMetadata inputActionPhaseMetadata = inputActionPhase.GetMetadata();
        Require(inputActionPhaseMetadata.IsEnum, "InputActionPhase is expected to be an enum.");
        Require(inputActionPhaseMetadata.IsValueType, "InputActionPhase enum is expected to be a value type.");

        Console.WriteLine($"InputSystem token:      0x{typeMetadata.MetadataToken:X8}");
        Console.WriteLine($"InputSystem attributes: {typeMetadata.Attributes}");
        Console.WriteLine($"QueueEvent token:       0x{methodMetadata.MetadataToken:X8}");
        Console.WriteLine($"QueueEvent attributes:  {methodMetadata.Attributes}");
        Console.WriteLine($"QueueEvent static:      {methodMetadata.IsStatic}");
        Console.WriteLine($"InputEventPtr size:     0x{inputEventPtrMetadata.ValueSize!.Value:X}");
        Console.WriteLine($"InputEventPtr align:    0x{inputEventPtrMetadata.ValueAlignment!.Value:X}");
        Console.WriteLine($"InputActionPhase enum:  {inputActionPhaseMetadata.IsEnum}");
        Console.WriteLine("Metadata cache:         OK");

        Pass();
    }

    /// <summary>
    /// Validates safe field-value reading for managed references, scalars, strings, vector arrays and conservative blittable-type guards.
    /// Dynamic field selection keeps the integration test resilient to non-contractual InputManager implementation details.
    /// </summary>
    /// <param name="assembly">The resolved Unity Input System assembly.</param>
    /// <param name="managerField">The resolved <c>InputSystem.s_Manager</c> static field.</param>
    /// <returns>One non-null array result when the live InputManager exposes a suitable array field; otherwise <see langword="null"/>.</returns>
    private static ResolvedArray? TestFieldValueReading(ResolvedAssembly assembly, ResolvedField managerField)
    {
        Section("Field value reading");

        nint managerAddress = managerField.ReadStaticReference();
        nint managerAddressOverride = managerField.ReadStaticReference(Il2CppClassLayouts.Class29_2X64);

        Require(managerAddress != 0, "s_Manager resolved to a null InputManager reference.");
        Require(managerAddress == managerAddressOverride, "Configured and one-shot layout paths returned different s_Manager references.");

        Console.WriteLine($"InputManager*: 0x{managerAddress:X}");
        Console.WriteLine("Static reference: OK");

        RequireThrows<InvalidOperationException>(
            () => managerField.ReadStaticString(),
            "A non-string managed reference was accepted by ReadStaticString.");
        RequireThrows<InvalidOperationException>(
            () => managerField.ReadStaticArray(),
            "A non-array managed reference was accepted by ReadStaticArray.");

        Console.WriteLine("Reference specialization checks: OK");

        ResolvedField? staticScalarField = managerField.DeclaringType.GetFields().FirstOrDefault(field => field.StorageKind == FieldStorageKind.Static && IsSupportedScalarFieldType(field.TypeName));

        if (staticScalarField is not null)
        {
            string staticValue = ReadStaticScalarAsText(staticScalarField);
            Console.WriteLine($"Static scalar: {staticScalarField.TypeName} {staticScalarField.Query.Name} = {staticValue}");
        }
        else
        {
            Console.WriteLine("No supported static scalar field was found on InputSystem; static scalar read test skipped.");
        }

        ResolvedType inputManager = assembly.ResolveType("UnityEngine.InputSystem", "InputManager");
        IReadOnlyList<ResolvedField> fields = inputManager.GetFields();
        ResolvedField? scalarField = fields.FirstOrDefault(IsSupportedScalarField);

        if (scalarField is not null)
        {
            string value = ReadScalarAsText(scalarField, managerAddress);
            Console.WriteLine($"Instance scalar: {scalarField.TypeName} {scalarField.Query.Name} = {value}");

            RequireThrows<InvalidOperationException>(
                () => ReadDeliberatelyWrongScalar(scalarField, managerAddress),
                "A deliberately incompatible managed scalar type was accepted for an instance field read.");
            RequireThrows<InvalidOperationException>(
                () => scalarField.ReadBlittable<int>(managerAddress),
                "A scalar field was accepted by the explicit blittable-structure API.");

            Console.WriteLine("Exact scalar validation: OK");
            Console.WriteLine("Type mismatch rejection: OK");
            Console.WriteLine("Blittable API separation: OK");
        }
        else
        {
            Console.WriteLine("No supported scalar instance field was found on InputManager; scalar read test skipped.");
        }

        ResolvedField? stringField = fields.FirstOrDefault(field => field.StorageKind == FieldStorageKind.Instance && string.Equals(field.TypeName, "System.String", StringComparison.Ordinal));

        if (stringField is not null)
        {
            string? value = stringField.ReadString(managerAddress);
            Console.WriteLine($"String field:   {stringField.Query.Name}");
            Console.WriteLine($"String value:   {FormatNullableString(value)}");
            Console.WriteLine("String decoding: OK");
        }
        else
        {
            Console.WriteLine("No System.String instance field was found on InputManager; string read test skipped.");
        }

        ResolvedField? arrayField = fields.FirstOrDefault(field => field.StorageKind == FieldStorageKind.Instance && field.TypeName.EndsWith("[]", StringComparison.Ordinal));
        ResolvedArray? array = null;

        if (arrayField is not null)
        {
            array = arrayField.ReadArray(managerAddress);
            Console.WriteLine($"Array field:    {arrayField.TypeName} {arrayField.Query.Name}");

            if (array is null)
            {
                Console.WriteLine("Array value:    <null>");
            }
            else
            {
                Require(array.Address != 0, "ResolvedArray has a null Il2CppArray*.");
                Require(array.Length >= 0, "ResolvedArray exposed a negative length.");
                Require(!string.IsNullOrWhiteSpace(array.ElementTypeName), "ResolvedArray exposed an empty element type name.");

                Console.WriteLine($"Array*:         0x{array.Address:X}");
                Console.WriteLine($"Length:         {array.Length}");
                Console.WriteLine($"Element type:   {array.ElementTypeName}");

                if (array.Length > 0)
                    Console.WriteLine($"First element:  {ReadArrayElementAsText(array)}");

                if (IsSupportedScalarFieldType(array.ElementTypeName))
                {
                    RequireThrows<ArgumentOutOfRangeException>(
                        () => ReadArrayScalarAt(array, array.Length),
                        "A scalar array accepted an index equal to Length.");
                    Console.WriteLine("Array bounds validation: OK");
                }
                else if (string.Equals(array.ElementTypeName, "System.String", StringComparison.Ordinal))
                {
                    RequireThrows<ArgumentOutOfRangeException>(
                        () => array.ReadString(array.Length),
                        "A string array accepted an index equal to Length.");
                    Console.WriteLine("Array bounds validation: OK");
                }
            }

            Console.WriteLine("Array inspection: OK");
        }
        else
        {
            Console.WriteLine("No vector-array instance field was found on InputManager; array read test skipped.");
        }

        Pass();
        return array;
    }

    /// <summary>
    /// Validates controlled parameterless property getter invocation for static scalars, managed references and instance scalars.
    /// The test prefers stable public Input System properties but retains dynamic scalar fallbacks for target-version tolerance.
    /// </summary>
    /// <param name="assembly">The resolved Unity Input System assembly.</param>
    /// <param name="inputSystem">The resolved static InputSystem type.</param>
    private static void TestPropertyValueReading(ResolvedAssembly assembly, ResolvedType inputSystem)
    {
        Section("Property value reading");

        IReadOnlyList<ResolvedProperty> properties = inputSystem.GetProperties();
        ResolvedProperty? staticScalar = properties.FirstOrDefault(property =>
            property.CanRead &&
            property.IndexParameterTypeNames.Count == 0 &&
            property.Getter is not null &&
            property.Getter.GetMetadata().IsStatic &&
            string.Equals(property.Query.Name, "pollingFrequency", StringComparison.Ordinal) &&
            IsSupportedScalarFieldType(property.TypeName));

        staticScalar ??= properties.FirstOrDefault(property =>
            property.CanRead &&
            property.IndexParameterTypeNames.Count == 0 &&
            property.Getter is not null &&
            property.Getter.GetMetadata().IsStatic &&
            IsSupportedScalarFieldType(property.TypeName));

        Require(staticScalar is not null, "No readable static scalar property was found on InputSystem.");
        string staticScalarValue = ReadStaticPropertyScalarAsText(staticScalar);
        Console.WriteLine($"Static scalar:  {staticScalar.TypeName} {staticScalar.Query.Name} = {staticScalarValue}");
        RequirePropertyScalarTypeMismatchRejected(staticScalar, 0, true);

        ResolvedProperty? settingsProperty = properties.FirstOrDefault(property =>
            property.CanRead &&
            property.IndexParameterTypeNames.Count == 0 &&
            property.Getter is not null &&
            property.Getter.GetMetadata().IsStatic &&
            string.Equals(property.Query.Name, "settings", StringComparison.Ordinal));

        if (settingsProperty is not null)
        {
            nint settingsAddress = settingsProperty.ReadStaticReference();
            Require(settingsAddress != 0, "InputSystem.settings returned a null managed reference.");
            RequireThrows<InvalidOperationException>(() => settingsProperty.ReadReference(settingsAddress), "A static property getter was accepted through the instance invocation API.");
            Console.WriteLine($"settings*:      0x{settingsAddress:X}");

            ResolvedType settingsType = assembly.ResolveType("UnityEngine.InputSystem", "InputSettings");
            IReadOnlyList<ResolvedProperty> settingsProperties = settingsType.GetProperties();
            ResolvedProperty? instanceScalar = settingsProperties.FirstOrDefault(property =>
                property.CanRead &&
                property.IndexParameterTypeNames.Count == 0 &&
                property.Getter is not null &&
                !property.Getter.GetMetadata().IsStatic &&
                string.Equals(property.Query.Name, "defaultButtonPressPoint", StringComparison.Ordinal) &&
                IsSupportedScalarFieldType(property.TypeName));

            instanceScalar ??= settingsProperties.FirstOrDefault(property =>
                property.CanRead &&
                property.IndexParameterTypeNames.Count == 0 &&
                property.Getter is not null &&
                !property.Getter.GetMetadata().IsStatic &&
                IsSupportedScalarFieldType(property.TypeName));

            if (instanceScalar is not null)
            {
                string value = ReadPropertyScalarAsText(instanceScalar, settingsAddress);
                Console.WriteLine($"Instance scalar:{instanceScalar.TypeName} {instanceScalar.Query.Name} = {value}");
                _ = ReadPropertyScalarAsText(instanceScalar, settingsAddress);
                _ = ReadPropertyScalarAsText(instanceScalar, settingsAddress);
                Console.WriteLine("Repeated rooted instance invocation: OK");
                RequirePropertyScalarTypeMismatchRejected(instanceScalar, settingsAddress, false);
                RequireStaticPropertyScalarRejected(instanceScalar);
                RequireNullPropertyInstanceRejected(instanceScalar);
            }
            else
                Console.WriteLine("No supported instance scalar property was found on InputSettings; instance getter test skipped.");
        }
        else
            Console.WriteLine("InputSystem.settings was not found; managed-reference and instance getter tests skipped.");

        Pass();
    }

    /// <summary>Reads one supported static scalar property getter and formats its result.</summary>
    /// <param name="property">The static scalar property.</param>
    /// <returns>The formatted scalar result.</returns>
    private static string ReadStaticPropertyScalarAsText(ResolvedProperty property)
    {
        return property.TypeName switch
        {
            "System.Boolean" => property.ReadStatic<bool>().ToString(),
            "System.Char" => ((int)property.ReadStatic<char>()).ToString(),
            "System.SByte" => property.ReadStatic<sbyte>().ToString(),
            "System.Byte" => property.ReadStatic<byte>().ToString(),
            "System.Int16" => property.ReadStatic<short>().ToString(),
            "System.UInt16" => property.ReadStatic<ushort>().ToString(),
            "System.Int32" => property.ReadStatic<int>().ToString(),
            "System.UInt32" => property.ReadStatic<uint>().ToString(),
            "System.Int64" => property.ReadStatic<long>().ToString(),
            "System.UInt64" => property.ReadStatic<ulong>().ToString(),
            "System.Single" => property.ReadStatic<float>().ToString("R"),
            "System.Double" => property.ReadStatic<double>().ToString("R"),
            "System.IntPtr" => $"0x{property.ReadStatic<nint>():X}",
            "System.UIntPtr" => $"0x{property.ReadStatic<nuint>():X}",
            _ => throw new InvalidOperationException($"Unsupported scalar property type '{property.TypeName}'.")
        };
    }

    /// <summary>Reads one supported instance scalar property getter and formats its result.</summary>
    /// <param name="property">The instance scalar property.</param>
    /// <param name="instanceAddress">The remote managed instance.</param>
    /// <returns>The formatted scalar result.</returns>
    private static string ReadPropertyScalarAsText(ResolvedProperty property, nint instanceAddress)
    {
        return property.TypeName switch
        {
            "System.Boolean" => property.Read<bool>(instanceAddress).ToString(),
            "System.Char" => ((int)property.Read<char>(instanceAddress)).ToString(),
            "System.SByte" => property.Read<sbyte>(instanceAddress).ToString(),
            "System.Byte" => property.Read<byte>(instanceAddress).ToString(),
            "System.Int16" => property.Read<short>(instanceAddress).ToString(),
            "System.UInt16" => property.Read<ushort>(instanceAddress).ToString(),
            "System.Int32" => property.Read<int>(instanceAddress).ToString(),
            "System.UInt32" => property.Read<uint>(instanceAddress).ToString(),
            "System.Int64" => property.Read<long>(instanceAddress).ToString(),
            "System.UInt64" => property.Read<ulong>(instanceAddress).ToString(),
            "System.Single" => property.Read<float>(instanceAddress).ToString("R"),
            "System.Double" => property.Read<double>(instanceAddress).ToString("R"),
            "System.IntPtr" => $"0x{property.Read<nint>(instanceAddress):X}",
            "System.UIntPtr" => $"0x{property.Read<nuint>(instanceAddress):X}",
            _ => throw new InvalidOperationException($"Unsupported scalar property type '{property.TypeName}'.")
        };
    }

    /// <summary>Verifies that a scalar property rejects a deliberately incompatible managed scalar type before result interpretation.</summary>
    /// <param name="property">The scalar property under test.</param>
    /// <param name="instanceAddress">The instance address for an instance getter, or zero for a static getter.</param>
    /// <param name="isStatic">Whether the property should be invoked through the static API.</param>
    private static void RequirePropertyScalarTypeMismatchRejected(ResolvedProperty property, nint instanceAddress, bool isStatic)
    {
        if (string.Equals(property.TypeName, "System.Int32", StringComparison.Ordinal))
        {
            if (isStatic)
                RequireThrows<InvalidOperationException>(() => property.ReadStatic<float>(), "An Int32 property was accepted as Single.");
            else
                RequireThrows<InvalidOperationException>(() => property.Read<float>(instanceAddress), "An Int32 property was accepted as Single.");

            return;
        }

        if (isStatic)
            RequireThrows<InvalidOperationException>(() => property.ReadStatic<int>(), $"Property type '{property.TypeName}' was accepted as Int32.");
        else
            RequireThrows<InvalidOperationException>(() => property.Read<int>(instanceAddress), $"Property type '{property.TypeName}' was accepted as Int32.");
    }

    /// <summary>Verifies that an instance scalar property cannot be invoked through the static getter API using its exact managed scalar type.</summary>
    /// <param name="property">The instance scalar property under test.</param>
    private static void RequireStaticPropertyScalarRejected(ResolvedProperty property)
    {
        switch (property.TypeName)
        {
            case "System.Boolean": RequireThrows<InvalidOperationException>(() => property.ReadStatic<bool>(), "Instance Boolean property was accepted as static."); break;
            case "System.Char": RequireThrows<InvalidOperationException>(() => property.ReadStatic<char>(), "Instance Char property was accepted as static."); break;
            case "System.SByte": RequireThrows<InvalidOperationException>(() => property.ReadStatic<sbyte>(), "Instance SByte property was accepted as static."); break;
            case "System.Byte": RequireThrows<InvalidOperationException>(() => property.ReadStatic<byte>(), "Instance Byte property was accepted as static."); break;
            case "System.Int16": RequireThrows<InvalidOperationException>(() => property.ReadStatic<short>(), "Instance Int16 property was accepted as static."); break;
            case "System.UInt16": RequireThrows<InvalidOperationException>(() => property.ReadStatic<ushort>(), "Instance UInt16 property was accepted as static."); break;
            case "System.Int32": RequireThrows<InvalidOperationException>(() => property.ReadStatic<int>(), "Instance Int32 property was accepted as static."); break;
            case "System.UInt32": RequireThrows<InvalidOperationException>(() => property.ReadStatic<uint>(), "Instance UInt32 property was accepted as static."); break;
            case "System.Int64": RequireThrows<InvalidOperationException>(() => property.ReadStatic<long>(), "Instance Int64 property was accepted as static."); break;
            case "System.UInt64": RequireThrows<InvalidOperationException>(() => property.ReadStatic<ulong>(), "Instance UInt64 property was accepted as static."); break;
            case "System.Single": RequireThrows<InvalidOperationException>(() => property.ReadStatic<float>(), "Instance Single property was accepted as static."); break;
            case "System.Double": RequireThrows<InvalidOperationException>(() => property.ReadStatic<double>(), "Instance Double property was accepted as static."); break;
            case "System.IntPtr": RequireThrows<InvalidOperationException>(() => property.ReadStatic<nint>(), "Instance IntPtr property was accepted as static."); break;
            case "System.UIntPtr": RequireThrows<InvalidOperationException>(() => property.ReadStatic<nuint>(), "Instance UIntPtr property was accepted as static."); break;
            default: throw new InvalidOperationException($"Unsupported scalar property type '{property.TypeName}'.");
        }
    }

    /// <summary>Verifies that an instance scalar property rejects a null managed instance before runtime invocation.</summary>
    /// <param name="property">The instance scalar property under test.</param>
    private static void RequireNullPropertyInstanceRejected(ResolvedProperty property)
    {
        switch (property.TypeName)
        {
            case "System.Boolean": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<bool>(0), "Instance Boolean property accepted a null managed instance."); break;
            case "System.Char": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<char>(0), "Instance Char property accepted a null managed instance."); break;
            case "System.SByte": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<sbyte>(0), "Instance SByte property accepted a null managed instance."); break;
            case "System.Byte": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<byte>(0), "Instance Byte property accepted a null managed instance."); break;
            case "System.Int16": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<short>(0), "Instance Int16 property accepted a null managed instance."); break;
            case "System.UInt16": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<ushort>(0), "Instance UInt16 property accepted a null managed instance."); break;
            case "System.Int32": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<int>(0), "Instance Int32 property accepted a null managed instance."); break;
            case "System.UInt32": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<uint>(0), "Instance UInt32 property accepted a null managed instance."); break;
            case "System.Int64": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<long>(0), "Instance Int64 property accepted a null managed instance."); break;
            case "System.UInt64": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<ulong>(0), "Instance UInt64 property accepted a null managed instance."); break;
            case "System.Single": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<float>(0), "Instance Single property accepted a null managed instance."); break;
            case "System.Double": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<double>(0), "Instance Double property accepted a null managed instance."); break;
            case "System.IntPtr": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<nint>(0), "Instance IntPtr property accepted a null managed instance."); break;
            case "System.UIntPtr": RequireThrows<ArgumentOutOfRangeException>(() => property.Read<nuint>(0), "Instance UIntPtr property accepted a null managed instance."); break;
            default: throw new InvalidOperationException($"Unsupported scalar property type '{property.TypeName}'.");
        }
    }

    /// <summary>Formats one nullable managed string without dumping unbounded remote content into integration-test output.</summary>
    /// <param name="value">The decoded string value.</param>
    /// <returns>A bounded diagnostic representation preserving null and empty values.</returns>
    private static string FormatNullableString(string? value)
    {
        if (value is null)
            return "<null>";

        if (value.Length == 0)
            return "<empty>";

        const int maximumDisplayedCharacters = 120;
        string bounded = value.Length <= maximumDisplayedCharacters ? value : value[..maximumDisplayedCharacters] + "…";
        return bounded.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>Reads one supported first array element into a bounded diagnostic string.</summary>
    /// <param name="array">The validated non-empty array.</param>
    /// <returns>A readable value or an explicit message when dynamic positive blittable/reference validation is intentionally unavailable.</returns>
    private static string ReadArrayElementAsText(ResolvedArray array)
    {
        if (IsSupportedScalarFieldType(array.ElementTypeName))
            return ReadArrayScalarAt(array, 0);

        if (string.Equals(array.ElementTypeName, "System.String", StringComparison.Ordinal))
            return FormatNullableString(array.ReadString(0));

        try
        {
            return $"reference 0x{array.ReadReference(0):X}";
        }
        catch (InvalidOperationException)
        {
            return "<value-type element; matching local blittable mirror required>";
        }
    }

    /// <summary>Reads one supported scalar array element and formats its value.</summary>
    /// <param name="array">The scalar array.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The formatted scalar value.</returns>
    private static string ReadArrayScalarAt(ResolvedArray array, int index)
    {
        return array.ElementTypeName switch
        {
            "System.Boolean" => array.Read<bool>(index).ToString(),
            "System.Char" => ((int)array.Read<char>(index)).ToString(),
            "System.SByte" => array.Read<sbyte>(index).ToString(),
            "System.Byte" => array.Read<byte>(index).ToString(),
            "System.Int16" => array.Read<short>(index).ToString(),
            "System.UInt16" => array.Read<ushort>(index).ToString(),
            "System.Int32" => array.Read<int>(index).ToString(),
            "System.UInt32" => array.Read<uint>(index).ToString(),
            "System.Int64" => array.Read<long>(index).ToString(),
            "System.UInt64" => array.Read<ulong>(index).ToString(),
            "System.Single" => array.Read<float>(index).ToString("R"),
            "System.Double" => array.Read<double>(index).ToString("R"),
            "System.IntPtr" => $"0x{array.Read<nint>(index):X}",
            "System.UIntPtr" => $"0x{array.Read<nuint>(index):X}",
            _ => throw new InvalidOperationException($"Unsupported scalar array element type '{array.ElementTypeName}'.")
        };
    }

    /// <summary>Determines whether a resolved field is an instance field whose semantic type belongs to the conservative scalar reader surface.</summary>
    /// <param name="field">The resolved field to inspect.</param>
    /// <returns><see langword="true"/> when the field can be exercised by the generic scalar integration test.</returns>
    private static bool IsSupportedScalarField(ResolvedField field)
    {
        if (field.StorageKind != FieldStorageKind.Instance)
            return false;

        return IsSupportedScalarFieldType(field.TypeName);
    }

    /// <summary>Determines whether a semantic field type belongs to the conservative scalar reader surface.</summary>
    /// <param name="typeName">The exact semantic managed type name.</param>
    /// <returns><see langword="true"/> when the type is supported by scalar field reading.</returns>
    private static bool IsSupportedScalarFieldType(string typeName)
    {
        return typeName is "System.Boolean" or
               "System.Char" or
               "System.SByte" or
               "System.Byte" or
               "System.Int16" or
               "System.UInt16" or
               "System.Int32" or
               "System.UInt32" or
               "System.Int64" or
               "System.UInt64" or
               "System.Single" or
               "System.Double" or
               "System.IntPtr" or
               "System.UIntPtr";
    }

    /// <summary>Reads one supported scalar static field and formats its value through the public generic field API.</summary>
    /// <param name="field">The supported scalar static field.</param>
    /// <returns>The formatted scalar value.</returns>
    private static string ReadStaticScalarAsText(ResolvedField field)
    {
        return field.TypeName switch
        {
            "System.Boolean" => field.ReadStatic<bool>().ToString(),
            "System.Char" => ((int)field.ReadStatic<char>()).ToString(),
            "System.SByte" => field.ReadStatic<sbyte>().ToString(),
            "System.Byte" => field.ReadStatic<byte>().ToString(),
            "System.Int16" => field.ReadStatic<short>().ToString(),
            "System.UInt16" => field.ReadStatic<ushort>().ToString(),
            "System.Int32" => field.ReadStatic<int>().ToString(),
            "System.UInt32" => field.ReadStatic<uint>().ToString(),
            "System.Int64" => field.ReadStatic<long>().ToString(),
            "System.UInt64" => field.ReadStatic<ulong>().ToString(),
            "System.Single" => field.ReadStatic<float>().ToString("R"),
            "System.Double" => field.ReadStatic<double>().ToString("R"),
            "System.IntPtr" => $"0x{field.ReadStatic<nint>():X}",
            "System.UIntPtr" => $"0x{field.ReadStatic<nuint>():X}",
            _ => throw new InvalidOperationException($"Unsupported static scalar test field type '{field.TypeName}'.")
        };
    }

    /// <summary>Reads one supported scalar instance field and formats the value without bypassing the public generic field API.</summary>
    /// <param name="field">The supported scalar instance field.</param>
    /// <param name="instanceAddress">The remote InputManager object address.</param>
    /// <returns>The formatted scalar value.</returns>
    private static string ReadScalarAsText(ResolvedField field, nint instanceAddress)
    {
        return field.TypeName switch
        {
            "System.Boolean" => field.Read<bool>(instanceAddress).ToString(),
            "System.Char" => ((int)field.Read<char>(instanceAddress)).ToString(),
            "System.SByte" => field.Read<sbyte>(instanceAddress).ToString(),
            "System.Byte" => field.Read<byte>(instanceAddress).ToString(),
            "System.Int16" => field.Read<short>(instanceAddress).ToString(),
            "System.UInt16" => field.Read<ushort>(instanceAddress).ToString(),
            "System.Int32" => field.Read<int>(instanceAddress).ToString(),
            "System.UInt32" => field.Read<uint>(instanceAddress).ToString(),
            "System.Int64" => field.Read<long>(instanceAddress).ToString(),
            "System.UInt64" => field.Read<ulong>(instanceAddress).ToString(),
            "System.Single" => field.Read<float>(instanceAddress).ToString("R"),
            "System.Double" => field.Read<double>(instanceAddress).ToString("R"),
            "System.IntPtr" => $"0x{field.Read<nint>(instanceAddress):X}",
            "System.UIntPtr" => $"0x{field.Read<nuint>(instanceAddress):X}",
            _ => throw new InvalidOperationException($"Unsupported scalar test field type '{field.TypeName}'.")
        };
    }

    /// <summary>Attempts one intentionally incompatible scalar read to verify that runtime type validation rejects size-compatible or arbitrary reinterpretation.</summary>
    /// <param name="field">The supported scalar instance field.</param>
    /// <param name="instanceAddress">The remote InputManager object address.</param>
    private static void ReadDeliberatelyWrongScalar(ResolvedField field, nint instanceAddress)
    {
        if (string.Equals(field.TypeName, "System.Int32", StringComparison.Ordinal))
        {
            _ = field.Read<float>(instanceAddress);
            return;
        }

        _ = field.Read<int>(instanceAddress);
    }

    /// <summary>
    /// Validates that targeted and navigational resolution paths reuse session-bound public objects by runtime identity.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="assembly">The previously resolved assembly.</param>
    /// <param name="type">The previously resolved type.</param>
    /// <param name="method">The previously resolved method.</param>
    /// <param name="property">The previously resolved property.</param>
    /// <param name="field">The previously resolved field.</param>
    private static void TestIdentityMap(Il2CppResolver resolver, ResolvedAssembly assembly, ResolvedType type, ResolvedMethod method, ResolvedProperty property, ResolvedField field)
    {
        Section("Identity map");

        ResolvedAssembly assemblyAgain = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
        ResolvedType typeAgain = resolver.ResolveType(new TypeQuery("Unity.InputSystem", "UnityEngine.InputSystem", "InputSystem"));
        ResolvedMethod methodAgain = resolver.ResolveMethod(new MethodQuery("Unity.InputSystem", "UnityEngine.InputSystem", "InputSystem", "QueueEvent", "UnityEngine.InputSystem.LowLevel.InputEventPtr"));
        ResolvedProperty propertyAgain = resolver.ResolveProperty(new PropertyQuery(type.Query, property.Query.Name, property.IndexParameterTypeNames.ToArray()));
        ResolvedField fieldAgain = resolver.ResolveField(new FieldQuery("Unity.InputSystem", "UnityEngine.InputSystem", "InputSystem", "s_Manager"));

        Require(ReferenceEquals(assembly, assemblyAgain), "ResolvedAssembly identity was not preserved.");
        Require(ReferenceEquals(type, typeAgain), "ResolvedType identity was not preserved.");
        Require(ReferenceEquals(method, methodAgain), "ResolvedMethod identity was not preserved.");
        Require(ReferenceEquals(property, propertyAgain), "ResolvedProperty identity was not preserved.");
        Require(ReferenceEquals(field, fieldAgain), "ResolvedField identity was not preserved.");

        Console.WriteLine("Assembly identity: OK");
        Console.WriteLine("Type identity:     OK");
        Console.WriteLine("Method identity:   OK");
        Console.WriteLine("Property identity: OK");
        Console.WriteLine("Field identity:    OK");

        Pass();
    }

    /// <summary>
    /// Validates that repeated navigation returns stable session snapshots and does not create duplicate public entities.
    /// </summary>
    /// <param name="assembly">The resolved assembly to enumerate repeatedly.</param>
    /// <param name="type">The resolved type to enumerate repeatedly.</param>
    private static void TestRepeatedNavigation(ResolvedAssembly assembly, ResolvedType type)
    {
        Section("Repeated navigation and cache reuse");

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<ResolvedType> types1 = assembly.GetTypes();
        stopwatch.Stop();
        long typesFirst = stopwatch.ElapsedTicks;

        stopwatch.Restart();
        IReadOnlyList<ResolvedType> types2 = assembly.GetTypes();
        stopwatch.Stop();
        long typesSecond = stopwatch.ElapsedTicks;

        Require(types1.Count == types2.Count, "Repeated GetTypes returned a different count.");

        ResolvedType? firstInputSystem = types1.FirstOrDefault(item => item.ClassAddress == type.ClassAddress);
        ResolvedType? secondInputSystem = types2.FirstOrDefault(item => item.ClassAddress == type.ClassAddress);

        Require(firstInputSystem is not null && secondInputSystem is not null, "InputSystem disappeared from repeated type enumeration.");
        Require(ReferenceEquals(firstInputSystem, secondInputSystem), "Repeated GetTypes created duplicate ResolvedType objects.");

        stopwatch.Restart();
        IReadOnlyList<ResolvedMethod> methods1 = type.GetMethods();
        stopwatch.Stop();
        long methodsFirst = stopwatch.ElapsedTicks;

        stopwatch.Restart();
        IReadOnlyList<ResolvedMethod> methods2 = type.GetMethods();
        stopwatch.Stop();
        long methodsSecond = stopwatch.ElapsedTicks;

        Require(methods1.Count == methods2.Count, "Repeated GetMethods returned a different count.");

        for (int index = 0; index < methods1.Count; index++)
            Require(ReferenceEquals(methods1[index], methods2[index]), $"Method identity changed at index {index}.");

        IReadOnlyList<ResolvedField> fields1 = type.GetFields();
        IReadOnlyList<ResolvedField> fields2 = type.GetFields();

        Require(fields1.Count == fields2.Count, "Repeated GetFields returned a different count.");

        for (int index = 0; index < fields1.Count; index++)
            Require(ReferenceEquals(fields1[index], fields2[index]), $"Field identity changed at index {index}.");

        IReadOnlyList<ResolvedProperty> properties1 = type.GetProperties();
        IReadOnlyList<ResolvedProperty> properties2 = type.GetProperties();

        Require(properties1.Count == properties2.Count, "Repeated GetProperties returned a different count.");

        for (int index = 0; index < properties1.Count; index++)
            Require(ReferenceEquals(properties1[index], properties2[index]), $"Property identity changed at index {index}.");

        Console.WriteLine($"GetTypes ticks:   first={typesFirst}, second={typesSecond}");
        Console.WriteLine($"GetMethods ticks: first={methodsFirst}, second={methodsSecond}");
        Console.WriteLine("Type identity reuse:   OK");
        Console.WriteLine("Method identity reuse: OK");
        Console.WriteLine("Field identity reuse:  OK");
        Console.WriteLine("Property identity reuse: OK");

        Console.WriteLine();
        Console.WriteLine("Timing is informational only; identity and result stability are the asserted cache invariants.");

        Pass();
    }

    /// <summary>
    /// Validates that <see cref="Il2CppResolver.ClearCache"/> advances the session generation and prevents previously resolved entities from performing further navigation.
    /// Explicitly configured layouts must remain usable after cache invalidation.
    /// </summary>
    /// <param name="resolver">The active resolver session.</param>
    /// <param name="assembly">A resolved assembly belonging to the previous generation.</param>
    /// <param name="type">A resolved type belonging to the previous generation.</param>
    /// <param name="method">A resolved method belonging to the previous generation.</param>
    /// <param name="property">A resolved property belonging to the previous generation.</param>
    /// <param name="field">A resolved field belonging to the previous generation.</param>
    /// <param name="array">An optional array wrapper belonging to the previous generation.</param>
    private static void TestCacheInvalidation(Il2CppResolver resolver, ResolvedAssembly assembly, ResolvedType type, ResolvedMethod method, ResolvedProperty property, ResolvedField field, ResolvedArray? array)
    {
        Section("Generation invalidation");

        resolver.ClearCache();

        RequireThrows<InvalidOperationException>(() => assembly.GetTypes(), "Old ResolvedAssembly remained navigable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => type.GetMethods(), "Old ResolvedType remained navigable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => type.GetBaseType(), "Old ResolvedType relationship navigation remained usable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => type.GetMetadata(), "Old ResolvedType metadata inspection remained usable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => method.ResolveCode(), "Old ResolvedMethod remained navigable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => method.GetMetadata(), "Old ResolvedMethod metadata inspection remained usable after ClearCache.");
        ResolvedMethod? oldPropertyAccessor = property.Getter ?? property.Setter;
        if (oldPropertyAccessor is not null)
            RequireThrows<InvalidOperationException>(() => oldPropertyAccessor.ResolveCode(), "Old property accessor remained navigable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => field.ResolveStorage(), "Old ResolvedField remained navigable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => property.ReadStaticReference(), "Old ResolvedProperty getter invocation remained usable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => field.ReadStaticReference(), "Old ResolvedField value reading remained usable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => field.ReadStaticString(), "Old ResolvedField string reading remained usable after ClearCache.");
        RequireThrows<InvalidOperationException>(() => field.ReadStaticArray(), "Old ResolvedField array reading remained usable after ClearCache.");

        if (array is not null)
            RequireThrows<InvalidOperationException>(() => array.ReadReference(0), "Old ResolvedArray remained readable after ClearCache.");

        Console.WriteLine("Old assembly rejected: OK");
        Console.WriteLine("Old type rejected:     OK");
        Console.WriteLine("Old relationships rejected: OK");
        Console.WriteLine("Old type metadata rejected: OK");
        Console.WriteLine("Old method rejected:   OK");
        Console.WriteLine("Old method metadata rejected: OK");
        Console.WriteLine("Old property accessor rejected: OK");
        Console.WriteLine("Old field rejected:    OK");
        Console.WriteLine("Old field value read rejected: OK");
        Console.WriteLine("Old property getter rejected: OK");
        Console.WriteLine("Old string/array field reads rejected: OK");
        Console.WriteLine($"Old array rejected:    {(array is null ? "SKIPPED" : "OK")}");

        // Snapshot properties remain valid.
        Require(assembly.ImageAddress != 0, "Old assembly snapshot lost its Image*.");
        Require(type.ClassAddress != 0, "Old type snapshot lost its Il2CppClass*.");
        Require(method.MethodInfoAddress != 0, "Old method snapshot lost its MethodInfo*.");
        Require(property.PropertyInfoAddress != 0, "Old property snapshot lost its PropertyInfo*.");
        Require(field.FieldInfoAddress != 0, "Old field snapshot lost its FieldInfo*.");

        Console.WriteLine("Snapshot properties:   OK");

        ResolvedType refreshedType = resolver.ResolveType(
            new TypeQuery(
                "Unity.InputSystem",
                "UnityEngine.InputSystem",
                "InputSystem"));

        ResolvedMethod refreshedMethod = refreshedType.ResolveMethod(
            "QueueEvent",
            "UnityEngine.InputSystem.LowLevel.InputEventPtr");

        ResolvedProperty refreshedProperty = refreshedType.ResolveProperty(property.Query.Name, property.IndexParameterTypeNames.ToArray());
        ResolvedField refreshedField = refreshedType.ResolveField("s_Manager");

        ResolvedMethodCode refreshedCode = refreshedMethod.ResolveCode();
        ResolvedFieldStorage refreshedStorage = refreshedField.ResolveStorage();
        ResolvedTypeMetadata refreshedTypeMetadata = refreshedType.GetMetadata();
        ResolvedMethodMetadata refreshedMethodMetadata = refreshedMethod.GetMetadata();
        ResolvedType? refreshedBaseType = refreshedType.GetBaseType();
        nint refreshedManagerAddress = refreshedField.ReadStaticReference();

        Require(!ReferenceEquals(type, refreshedType), "ClearCache did not create a new ResolvedType generation.");
        Require(!ReferenceEquals(method, refreshedMethod), "ClearCache did not create a new ResolvedMethod generation.");
        Require(!ReferenceEquals(property, refreshedProperty), "ClearCache did not create a new ResolvedProperty generation.");
        Require(!ReferenceEquals(field, refreshedField), "ClearCache did not create a new ResolvedField generation.");

        Require(refreshedCode.NativeAddress != 0, "Configured MethodInfo layout was lost after ClearCache.");
        Require(refreshedStorage.StorageAddress != 0, "Configured field-storage layout was lost after ClearCache.");
        Require(refreshedTypeMetadata.MetadataToken != 0, "Type metadata did not rematerialize after ClearCache.");
        Require(refreshedMethodMetadata.MetadataToken != 0, "Method metadata did not rematerialize after ClearCache.");
        Require(refreshedBaseType is not null, "Type relationships did not rematerialize after ClearCache.");
        Require(refreshedManagerAddress != 0, "Field value reading did not rematerialize after ClearCache.");

        Console.WriteLine("New generation created:       OK");
        Console.WriteLine("Method layout survived cache: OK");
        Console.WriteLine("Field layout survived cache:  OK");
        Console.WriteLine("Metadata rematerialized:      OK");
        Console.WriteLine("Relationships rematerialized: OK");
        Console.WriteLine("Field value reading restored: OK");

        Pass();
    }

    /// <summary>Formats a resolved property as a readable managed signature for integration-test output.</summary>
    /// <param name="property">The resolved property to format.</param>
    /// <returns>A readable property signature including index parameters.</returns>
    private static string FormatProperty(ResolvedProperty property)
    {
        string indexParameters = string.Join(", ", property.IndexParameterTypeNames);
        string suffix = property.IndexParameterTypeNames.Count == 0 ? string.Empty : $"[{indexParameters}]";
        return $"{property.TypeName} {property.Query.Name}{suffix}";
    }

    /// <summary>Determines whether two ordered semantic type-name sequences are identical.</summary>
    /// <param name="left">The first ordered type-name sequence.</param>
    /// <param name="right">The second ordered type-name sequence.</param>
    /// <returns><see langword="true"/> when both sequences match exactly.</returns>
    private static bool ParametersEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
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
    /// Formats a resolved method as a readable managed signature for integration-test output.
    /// </summary>
    /// <param name="method">The resolved method to format.</param>
    /// <returns>A readable method signature.</returns>
    private static string FormatMethod(ResolvedMethod method)
    {
        string parameters = string.Join(", ", method.ParameterTypeNames);
        return $"{method.ReturnTypeName} {method.Query.Name}({parameters})";
    }

    /// <summary>
    /// Writes a visual section header to the integration-test output.
    /// </summary>
    /// <param name="name">The section name to display.</param>
    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {name} ---");
    }

    /// <summary>
    /// Writes a successful validation marker to the integration-test output.
    /// </summary>
    private static void Pass()
    {
        Console.WriteLine("[PASS]");
    }

    /// <summary>
    /// Verifies an integration-test invariant and throws immediately when it is violated.
    /// </summary>
    /// <param name="condition">The condition that must evaluate to <see langword="true"/>.</param>
    /// <param name="message">The diagnostic message reported when the invariant fails.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Verifies that an operation throws the expected exception type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The operation expected to fail.</param>
    /// <param name="message">The diagnostic message reported when the expected exception is not observed.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation does not produce the expected exception type.
    /// </exception>
    private static void RequireThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}