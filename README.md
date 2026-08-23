# Unity IL2CPP Resolver

`Il2CppResolver` is a C# API for exploring and reading a live Windows x64 Unity IL2CPP game.

You do **not** need an IL2CPP dump to start using it. If all you have is a running game, the resolver can enumerate the assemblies and types exposed by the live IL2CPP runtime, then let you navigate their methods, properties, fields, metadata, relationships, and supported runtime values.

A typical workflow looks like this:

```text
Running Unity game
        ↓
Find the process
        ↓
Attach Il2CppResolver
        ↓
Explore assemblies and types
        ↓
Inspect fields / properties / methods
        ↓
Resolve the member you need
        ↓
Read a value or follow an object reference
```

This README is intentionally written as a **consumer guide**. It focuses on what the public API does, when to use it, and how to use it from normal C# code.

For implementation details such as remote process access, IL2CPP runtime calls, ABI handling, GC rooting, cache internals, layout detection, and safety decisions, see [tech-doc.md](tech-doc.md).

---

## Table of Contents

- [1. Requirements](#1-requirements)
- [2. First connection to a running game](#2-first-connection-to-a-running-game)
- [3. No dump? Explore the live game first](#3-no-dump-explore-the-live-game-first)
- [4. Resolve known assemblies, types and members](#4-resolve-known-assemblies-types-and-members)
- [5. Understand resolved entities](#5-understand-resolved-entities)
- [6. Inspect types and relationships](#6-inspect-types-and-relationships)
- [7. Inspect methods and resolve native code](#7-inspect-methods-and-resolve-native-code)
- [8. Read fields](#8-read-fields)
- [9. Follow managed object references](#9-follow-managed-object-references)
- [10. Read properties](#10-read-properties)
- [11. Read arrays](#11-read-arrays)
- [12. Map IL2CPP values to C#](#12-map-il2cpp-values-to-c)
- [13. Work with static-field storage and layouts](#13-work-with-static-field-storage-and-layouts)
- [14. Cache, identity and generations](#14-cache-identity-and-generations)
- [15. Errors and common mistakes](#15-errors-and-common-mistakes)
- [16. Complete exploration workflow](#16-complete-exploration-workflow)
- [17. API coverage index](#17-api-coverage-index)
- [18. Current limitations](#18-current-limitations)
- [19. Technical documentation](#19-technical-documentation)

---

# 1. Requirements

The current resolver targets:

- Windows;
- .NET 8;
- a 64-bit resolver process;
- a native Windows x64 Unity IL2CPP target;
- a target exposing an accessible `GameAssembly.dll`.

Normal consumer code usually needs these namespaces:

```csharp
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Invocation;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
```

Most examples in this guide use `Dofus` as the process name and `Unity.InputSystem` as a real assembly that is commonly present in Unity games. Replace those names with values discovered from your own target when necessary.

A few examples use representative game types such as `Assembly-CSharp`, `Game.Player`, `Game.PlayerState`, or `Game.Math.Position`. These names are intentionally illustrative: the [live navigation workflow](#3-no-dump-explore-the-live-game-first) shows how to discover the equivalent types and members in your own game without a dump.

## Minimal vocabulary before you start

You do not need to understand IL2CPP internals to use the resolver. The consumer API intentionally exposes familiar managed concepts:

| Term | Meaning in this guide |
| --- | --- |
| **Assembly** | A group of managed types, for example `Assembly-CSharp` or `Unity.InputSystem.dll` |
| **Type** | A class, struct, enum, or interface |
| **Field** | Stored data belonging to an object or a type |
| **Property** | A managed accessor such as `Health { get; }`; reading it executes its supported getter |
| **Method** | A managed function; the resolver can inspect it and resolve native code, but does not generally invoke it |
| **Instance** | One concrete managed object in the running game |
| **`nint` object address** | The remote address of a managed object inside the game process; pass it back to instance-read APIs rather than dereferencing it locally |
| **Query** | A semantic description of something you want to resolve by name |
| **`Resolved*` object** | The resolver's session-bound representation of a live runtime entity |

Unity IL2CPP takes managed game code and produces native code, but the running runtime still exposes enough managed identity to navigate assemblies, types, methods, properties, and fields. `Il2CppResolver` uses that live information so the consumer can work with names and types instead of starting from raw offsets.

---

# 2. First connection to a running game

The first thing the resolver needs is the process ID of the running Unity game.

For a beginner, finding it by executable name is usually easier than manually copying a PID from Task Manager.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;

// Replace "Dofus" with the executable name of your target game.
// Process.GetProcessesByName expects the name without ".exe".
int processId = Process.GetProcessesByName("Dofus")[0].Id;

// Attach creates one resolver session for this process.
// Always dispose the resolver when you are done with it.
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// ProcessId lets you confirm which process this resolver owns.
Console.WriteLine($"Attached to PID {resolver.ProcessId}");
```

If you want a safer process lookup, check that the game was found before attaching:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;

Process[] processes = Process.GetProcessesByName("Dofus");

if (processes.Length == 0)
    throw new InvalidOperationException("The target game is not running.");

int processId = processes[0].Id;

// The second Attach overload changes the maximum duration of one individual
// remote runtime call. The default overload is enough for normal usage.
using Il2CppResolver resolver = Il2CppResolver.Attach(processId, TimeSpan.FromSeconds(3));

Console.WriteLine($"Attached to PID {resolver.ProcessId}");
```

Use the timeout overload when the default timeout is not appropriate for the target. A timeout applies to individual runtime operations, not to the complete lifetime of the resolver.

---

# 3. No dump? Explore the live game first

This is one of the most useful parts of the project.

If you have **no dump, no generated C# assemblies, no symbol list, and no prior knowledge of the game's IL2CPP metadata**, you can still start from the live process and navigate what the runtime exposes.

The discovery path is:

```text
GetAssemblies()
    ↓
ResolvedAssembly.GetTypes()
    ↓
ResolvedType.GetFields()
ResolvedType.GetProperties()
ResolvedType.GetMethods()
    ↓
inspect names, signatures and metadata
```

The following example starts only from the running process and explores the target interactively:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Results;
using System.Linq;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// STEP 1: enumerate every assembly currently registered in the live IL2CPP domain.
IReadOnlyList<ResolvedAssembly> assemblies = resolver.GetAssemblies();

Console.WriteLine($"Assemblies: {assemblies.Count}");

foreach (ResolvedAssembly assembly in assemblies)
    Console.WriteLine(assembly.Name);

// STEP 2: choose an interesting assembly and enumerate its types.
// Unity.InputSystem is used here because it exists in the target used to validate the resolver.
ResolvedAssembly inputSystemAssembly = assemblies.First(assembly =>
    string.Equals(assembly.Name, "Unity.InputSystem.dll", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(assembly.Name, "Unity.InputSystem", StringComparison.OrdinalIgnoreCase));

IReadOnlyList<ResolvedType> types = inputSystemAssembly.GetTypes();
Console.WriteLine($"Types in {inputSystemAssembly.Name}: {types.Count}");

// Printing every type can be noisy, so filtering by a word is often more useful.
foreach (ResolvedType type in types.Where(type => type.Query.Name.Contains("InputSystem", StringComparison.OrdinalIgnoreCase)))
    Console.WriteLine($"{type.Query.Namespace}.{type.Query.Name}");

// STEP 3: once you see an interesting type, inspect all of its members.
ResolvedType inputSystem = inputSystemAssembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

Console.WriteLine("\nFields:");
foreach (ResolvedField field in inputSystem.GetFields())
    Console.WriteLine($"  {field.TypeName} {field.Query.Name} [{field.StorageKind}]");

Console.WriteLine("\nProperties:");
foreach (ResolvedProperty property in inputSystem.GetProperties())
    Console.WriteLine($"  {property.TypeName} {property.Query.Name} (read={property.CanRead}, write={property.CanWrite})");

Console.WriteLine("\nMethods:");
foreach (ResolvedMethod method in inputSystem.GetMethods())
{
    string parameters = string.Join(", ", method.ParameterTypeNames);
    Console.WriteLine($"  {method.ReturnTypeName} {method.Query.Name}({parameters})");
}
```

### Why this matters

A traditional memory-reading workflow often starts with offsets produced by an external dump. `Il2CppResolver` can instead use the live runtime as the source of semantic information.

That means you can progressively answer questions such as:

- Which assemblies are loaded?
- Does the game use `Assembly-CSharp`, a custom assembly, or a Unity package?
- Which namespace contains a type whose name looks relevant?
- What fields does that type expose?
- Which fields are static and which belong to instances?
- What are the field types?
- Which properties expose getters?
- Which method overloads exist?
- Is a runtime type a class, struct, enum, interface, abstract type, or sealed type?

You can then replace broad enumeration with targeted resolution once you know the semantic names you want.

> `ResolvedAssembly.GetTypes()` depends on optional IL2CPP enumeration capabilities. If the target does not expose them, targeted resolution can still be available when you already know the assembly/type identity.

---

# 4. Resolve known assemblies, types and members

Once you know a semantic identity, use queries or navigation shortcuts instead of repeatedly enumerating everything.

Queries are immutable C# objects. They describe **what** you want to resolve; they do not contain process addresses or native offsets.

The complete query model can be learned in one workflow:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// AssemblyQuery identifies one managed assembly by name.
AssemblyQuery assemblyQuery = new("Unity.InputSystem");
Console.WriteLine(assemblyQuery.Name);

// TypeQuery identifies one type by assembly + namespace + managed type name.
TypeQuery inputSystemQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem");

Console.WriteLine(inputSystemQuery.Assembly.Name);
Console.WriteLine(inputSystemQuery.Namespace);
Console.WriteLine(inputSystemQuery.Name);

// MethodQuery can be created with the complete declaring-type identity...
MethodQuery fullMethodQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

// ...or from an existing TypeQuery to avoid repeating the same type identity.
MethodQuery methodQuery = new(
    inputSystemQuery,
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

Console.WriteLine(methodQuery.DeclaringType.Name);
Console.WriteLine(methodQuery.Name);
Console.WriteLine(string.Join(", ", methodQuery.ParameterTypeNames));

// PropertyQuery follows the same pattern. Extra type names identify indexer parameters.
PropertyQuery fullPropertyQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "pollingFrequency");

PropertyQuery propertyQuery = new(inputSystemQuery, "pollingFrequency");
Console.WriteLine(propertyQuery.DeclaringType.Name);
Console.WriteLine(propertyQuery.Name);
Console.WriteLine(propertyQuery.IndexParameterTypeNames.Count); // 0 for a normal property.

// For an indexer, pass the ordered index-parameter type names.
PropertyQuery indexerQuery = new(
    new TypeQuery("Some.Assembly", "Some.Namespace", "SomeCollection"),
    "Item",
    "System.Int32");

// FieldQuery also supports both complete and TypeQuery-based construction.
FieldQuery fullFieldQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");

FieldQuery fieldQuery = new(inputSystemQuery, "s_Manager");
Console.WriteLine(fieldQuery.DeclaringType.Name);
Console.WriteLine(fieldQuery.Name);

// The resolver can resolve each query directly.
ResolvedAssembly assembly = resolver.ResolveAssembly(assemblyQuery);
ResolvedType type = resolver.ResolveType(inputSystemQuery);
ResolvedMethod method = resolver.ResolveMethod(methodQuery);
ResolvedProperty property = resolver.ResolveProperty(propertyQuery);
ResolvedField field = resolver.ResolveField(fieldQuery);

Console.WriteLine(assembly.Name);
Console.WriteLine(type.Query.Name);
Console.WriteLine(method.Query.Name);
Console.WriteLine(property.Query.Name);
Console.WriteLine(field.Query.Name);

// Once a type is already resolved, the shorter navigation endpoints are usually clearer.
ResolvedMethod sameMethod = type.ResolveMethod("QueueEvent", "UnityEngine.InputSystem.LowLevel.InputEventPtr");
ResolvedProperty sameProperty = type.ResolveProperty("pollingFrequency");
ResolvedField sameField = type.ResolveField("s_Manager");

Console.WriteLine(ReferenceEquals(method, sameMethod));
Console.WriteLine(ReferenceEquals(property, sameProperty));
Console.WriteLine(ReferenceEquals(field, sameField));
```

### Which approach should you use?

Use the resolver-level query endpoints when you have a complete semantic identity and do not already hold the declaring type.

Use navigation endpoints such as `assembly.ResolveType(...)`, `type.ResolveMethod(...)`, `type.ResolveProperty(...)`, and `type.ResolveField(...)` when you already navigated to the parent object. They are shorter and make the relationship explicit.

For methods and indexers, parameter type names are ordered and are part of overload identity.

---

# 5. Understand resolved entities

A successful resolution returns a `Resolved*` object. These objects expose both semantic information and the current remote runtime identity.

You normally do not need the raw addresses to read values, but they are useful for diagnostics and interoperability.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType type = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");
ResolvedMethod method = type.ResolveMethod("QueueEvent", "UnityEngine.InputSystem.LowLevel.InputEventPtr");
ResolvedProperty property = type.ResolveProperty("pollingFrequency");
ResolvedField field = type.ResolveField("s_Manager");

// ResolvedAssembly
Console.WriteLine(assembly.Query.Name);
Console.WriteLine(assembly.Name);
Console.WriteLine($"Assembly*: 0x{assembly.AssemblyAddress:X}");
Console.WriteLine($"Image*:    0x{assembly.ImageAddress:X}");

// ResolvedType
Console.WriteLine(type.Query.Assembly.Name);
Console.WriteLine(type.Query.Namespace);
Console.WriteLine(type.Query.Name);
Console.WriteLine($"Il2CppClass*: 0x{type.ClassAddress:X}");

// ResolvedMethod
Console.WriteLine(method.DeclaringType.Query.Name);
Console.WriteLine(method.ReturnTypeName);
Console.WriteLine(string.Join(", ", method.ParameterTypeNames));
Console.WriteLine($"MethodInfo*: 0x{method.MethodInfoAddress:X}");

// ResolvedProperty
Console.WriteLine(property.DeclaringType.Query.Name);
Console.WriteLine(property.TypeName);
Console.WriteLine(string.Join(", ", property.IndexParameterTypeNames));
Console.WriteLine(property.Attributes);
Console.WriteLine(property.CanRead);
Console.WriteLine(property.CanWrite);
Console.WriteLine($"PropertyInfo*: 0x{property.PropertyInfoAddress:X}");

// Getter and Setter are regular ResolvedMethod objects when the accessors exist.
if (property.Getter is not null)
    Console.WriteLine($"Getter MethodInfo*: 0x{property.Getter.MethodInfoAddress:X}");

if (property.Setter is not null)
    Console.WriteLine($"Setter MethodInfo*: 0x{property.Setter.MethodInfoAddress:X}");

// CanWrite only means that IL2CPP metadata exposes a setter.
// The current resolver does not invoke property setters.

// ResolvedField
Console.WriteLine(field.DeclaringType.Query.Name);
Console.WriteLine(field.TypeName);
Console.WriteLine(field.Attributes);
Console.WriteLine(field.StorageKind);
Console.WriteLine(field.InstanceOffset);
Console.WriteLine(field.StaticStorageOffset);
Console.WriteLine($"FieldInfo*: 0x{field.FieldInfoAddress:X}");
```

The raw addresses are addresses **inside the target process**, not local pointers that should be dereferenced directly by your C# process.

---

# 6. Inspect types and relationships

`ResolvedType` is the main navigation object when exploring a game. It can enumerate members, resolve specific members, navigate relationships, and expose runtime metadata.

The following example demonstrates the complete type-level API:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType inputSystem = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

// Enumerate every method, or only methods sharing one name.
IReadOnlyList<ResolvedMethod> allMethods = inputSystem.GetMethods();
IReadOnlyList<ResolvedMethod> queueEventOverloads = inputSystem.GetMethods("QueueEvent");

// Enumerate every property, or only properties sharing one name.
IReadOnlyList<ResolvedProperty> allProperties = inputSystem.GetProperties();
IReadOnlyList<ResolvedProperty> settingsProperties = inputSystem.GetProperties("settings");

// Enumerate fields.
IReadOnlyList<ResolvedField> allFields = inputSystem.GetFields();

Console.WriteLine($"Methods:    {allMethods.Count}");
Console.WriteLine($"QueueEvent: {queueEventOverloads.Count}");
Console.WriteLine($"Properties: {allProperties.Count}");
Console.WriteLine($"settings:   {settingsProperties.Count}");
Console.WriteLine($"Fields:     {allFields.Count}");

// Navigate type relationships.
ResolvedType? baseType = inputSystem.GetBaseType();
IReadOnlyList<ResolvedType> interfaces = inputSystem.GetInterfaces();
IReadOnlyList<ResolvedType> nestedTypes = inputSystem.GetNestedTypes();
ResolvedType? declaringType = inputSystem.GetDeclaringType();

Console.WriteLine($"Base: {baseType?.Query.Namespace}.{baseType?.Query.Name}");
Console.WriteLine($"Interfaces: {interfaces.Count}");
Console.WriteLine($"Nested types: {nestedTypes.Count}");
Console.WriteLine($"Declaring type: {declaringType?.Query.Name ?? "<none>"}");

// GetMetadata tells you what kind of managed type you resolved.
ResolvedTypeMetadata metadata = inputSystem.GetMetadata();

Console.WriteLine($"Attributes: {metadata.Attributes}");
Console.WriteLine($"Token: 0x{metadata.MetadataToken:X8}");
Console.WriteLine($"Value type: {metadata.IsValueType}");
Console.WriteLine($"Enum: {metadata.IsEnum}");
Console.WriteLine($"Interface: {metadata.IsInterface}");
Console.WriteLine($"Abstract: {metadata.IsAbstract}");
Console.WriteLine($"Sealed: {metadata.IsSealed}");
Console.WriteLine($"Blittable: {metadata.IsBlittable}");
Console.WriteLine($"Generic: {metadata.IsGeneric}");
Console.WriteLine($"Inflated generic: {metadata.IsInflated}");
Console.WriteLine($"Value size: {metadata.ValueSize?.ToString() ?? "<not a value type>"}");
Console.WriteLine($"Value alignment: {metadata.ValueAlignment?.ToString() ?? "<not available>"}");
```

### Identify classes, structs, enums and interfaces

You do not need a dump to determine the broad category of a runtime type:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType inputEventPtr = resolver.ResolveType(new TypeQuery(
    "Unity.InputSystem",
    "UnityEngine.InputSystem.LowLevel",
    "InputEventPtr"));

ResolvedTypeMetadata structMetadata = inputEventPtr.GetMetadata();
bool isStruct = structMetadata.IsValueType && !structMetadata.IsEnum;

Console.WriteLine($"InputEventPtr is a struct: {isStruct}");
Console.WriteLine($"Size: {structMetadata.ValueSize}");
Console.WriteLine($"Alignment: {structMetadata.ValueAlignment}");
Console.WriteLine($"Blittable: {structMetadata.IsBlittable}");

ResolvedType inputActionPhase = resolver.ResolveType(new TypeQuery(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputActionPhase"));

ResolvedTypeMetadata enumMetadata = inputActionPhase.GetMetadata();
Console.WriteLine($"InputActionPhase is an enum: {enumMetadata.IsEnum}");
```

A normal struct is `IsValueType == true` and `IsEnum == false`. An enum also reports itself as a value type, so always check `IsEnum` separately.

---

# 7. Inspect methods and resolve native code

The resolver can identify exact managed overloads, inspect method metadata, and map a `MethodInfo*` to validated executable native code.

It does **not** currently expose general `ResolvedMethod.Invoke(...)` support.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType inputSystem = resolver.ResolveType(new TypeQuery(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem"));

// GetMethods(name) is useful when you know a method name but not its overload signature.
IReadOnlyList<ResolvedMethod> overloads = inputSystem.GetMethods("QueueEvent");

foreach (ResolvedMethod overload in overloads)
{
    string parameters = string.Join(", ", overload.ParameterTypeNames);
    Console.WriteLine($"{overload.ReturnTypeName} {overload.Query.Name}({parameters})");
}

// ResolveMethod selects one exact overload from its ordered parameter type names.
ResolvedMethod queueEvent = inputSystem.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

ResolvedMethodMetadata metadata = queueEvent.GetMetadata();
Console.WriteLine($"Attributes: {metadata.Attributes}");
Console.WriteLine($"Implementation attributes: {metadata.ImplementationAttributes}");
Console.WriteLine($"Token: 0x{metadata.MetadataToken:X8}");
Console.WriteLine($"Static: {metadata.IsStatic}");
Console.WriteLine($"Instance: {metadata.IsInstance}");
Console.WriteLine($"Public: {metadata.IsPublic}");
Console.WriteLine($"Virtual: {metadata.IsVirtual}");
Console.WriteLine($"Abstract: {metadata.IsAbstract}");
Console.WriteLine($"Final: {metadata.IsFinal}");
Console.WriteLine($"Generic: {metadata.IsGeneric}");
Console.WriteLine($"Inflated: {metadata.IsInflated}");

// Native-code resolution needs a MethodInfo layout. You can select a known layout globally.
resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

ResolvedMethodCode codeFromMethod = queueEvent.ResolveCode();

Console.WriteLine($"Native address: 0x{codeFromMethod.NativeAddress:X}");
Console.WriteLine($"PE section: {codeFromMethod.SectionName}");
Console.WriteLine($"Compatibility profile: {codeFromMethod.CompatibilityProfile}");
Console.WriteLine($"Method pointer offset: 0x{codeFromMethod.MethodPointerOffset:X}");
Console.WriteLine($"Resolved method: {codeFromMethod.Method.Query.Name}");

// The resolver-level endpoint does the same operation directly from a query.
MethodQuery queueEventQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

ResolvedMethodCode codeFromResolver = resolver.ResolveMethodCode(queueEventQuery);

// A one-shot layout override applies only to this call and does not change MethodInfoLayout.
ResolvedMethodCode oneShotFromMethod = queueEvent.ResolveCode(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
ResolvedMethodCode oneShotFromResolver = resolver.ResolveMethodCode(queueEventQuery, Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

Console.WriteLine($"Selected global layout: {resolver.MethodInfoLayout?.Name}");
Console.WriteLine($"All paths agree: {codeFromMethod.NativeAddress == codeFromResolver.NativeAddress && codeFromMethod.NativeAddress == oneShotFromMethod.NativeAddress && codeFromMethod.NativeAddress == oneShotFromResolver.NativeAddress}");

// Reset removes the explicit selection and restores automatic MethodInfo layout selection.
resolver.ResetMethodInfoLayout();
Console.WriteLine(resolver.MethodInfoLayout is null);
```

If no MethodInfo layout is explicitly selected, the default code-resolution path can attempt automatic selection from registered candidates and runtime evidence. If the target cannot provide enough evidence, configure a known layout explicitly.

---

# 8. Read fields

Fields are the main direct-memory reading surface.

The first question to ask is **where the field is stored**. `StorageKind` distinguishes instance fields, normal static fields, thread-static fields, and literals.

## Read a real static reference, then inspect an instance

This example uses the tested `UnityEngine.InputSystem.InputSystem.s_Manager` field. It demonstrates `ReadStaticReference()`, instance scalar reads, strings, arrays, and field storage metadata without requiring a dump.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;
using System.Linq;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// The validation target used by the project is compatible with this class layout.
// Your target may use a different layout or expose runtime static-storage APIs instead.
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType inputSystem = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");
ResolvedField managerField = inputSystem.ResolveField("s_Manager");

Console.WriteLine($"Field type: {managerField.TypeName}");
Console.WriteLine($"Storage kind: {managerField.StorageKind}");
Console.WriteLine($"Static offset: 0x{managerField.StaticStorageOffset:X}");

// s_Manager is a managed object reference stored in a static field.
// ReadStaticReference returns the remote Il2CppObject* as an nint.
nint managerAddress = managerField.ReadStaticReference();
Console.WriteLine($"InputManager*: 0x{managerAddress:X}");

// Resolve the runtime type of the object we just obtained and inspect its fields.
ResolvedType inputManager = assembly.ResolveType("UnityEngine.InputSystem", "InputManager");
IReadOnlyList<ResolvedField> managerFields = inputManager.GetFields();

// Find a supported scalar instance field dynamically so this example is not tied
// to a private field name that may change between Input System versions.
string[] scalarTypeNames =
{
    "System.Boolean", "System.Char", "System.SByte", "System.Byte",
    "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
    "System.Int64", "System.UInt64", "System.Single", "System.Double",
    "System.IntPtr", "System.UIntPtr"
};

ResolvedField? scalarField = managerFields.FirstOrDefault(field =>
    field.StorageKind == FieldStorageKind.Instance && scalarTypeNames.Contains(field.TypeName));

if (scalarField is not null)
{
    // Read<T>(instanceAddress) is for instance scalar fields.
    // Choose T according to the exact IL2CPP field type.
    if (scalarField.TypeName == "System.Boolean")
        Console.WriteLine($"{scalarField.Query.Name} = {scalarField.Read<bool>(managerAddress)}");
    else if (scalarField.TypeName == "System.Int32")
        Console.WriteLine($"{scalarField.Query.Name} = {scalarField.Read<int>(managerAddress)}");
    else if (scalarField.TypeName == "System.Single")
        Console.WriteLine($"{scalarField.Query.Name} = {scalarField.Read<float>(managerAddress)}");
    else
        Console.WriteLine($"Found scalar field {scalarField.TypeName} {scalarField.Query.Name}; use the matching C# scalar type from the mapping table below.");
}

// String fields have a specialized reader that decodes System.String for you.
ResolvedField? stringField = managerFields.FirstOrDefault(field =>
    field.StorageKind == FieldStorageKind.Instance &&
    string.Equals(field.TypeName, "System.String", StringComparison.Ordinal));

if (stringField is not null)
{
    string? value = stringField.ReadString(managerAddress);
    Console.WriteLine($"{stringField.Query.Name} = {value ?? "<null>"}");
}

// Array fields are materialized as ResolvedArray rather than copied blindly.
ResolvedField? arrayField = managerFields.FirstOrDefault(field =>
    field.StorageKind == FieldStorageKind.Instance &&
    field.TypeName.EndsWith("[]", StringComparison.Ordinal));

if (arrayField is not null)
{
    ResolvedArray? array = arrayField.ReadArray(managerAddress);

    if (array is not null)
    {
        Console.WriteLine($"{arrayField.Query.Name}: length={array.Length}, element={array.ElementTypeName}");
    }
}
```

## Every field value category

Once navigation has shown you the field type and storage kind, choose the matching reader. The following representative game model demonstrates every default field-reading endpoint in one workflow:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Representative members discovered through navigation:
// Assembly-CSharp
//   Game.GameState
//     static int GlobalPlayerCount
//     static Game.Player CurrentPlayer
//     static Game.PlayerState GlobalState
//     static string ServerName
//     static Game.Player[] Players
//     static Game.Math.Position SpawnPosition
//
//   Game.Player
//     int Health
//     Game.Player Target
//     Game.PlayerState State
//     string Name
//     Game.Item[] Items
//     Game.Math.Position Position

ResolvedType gameStateType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "GameState"));
ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));

// Static scalar.
int globalPlayerCount = gameStateType.ResolveField("GlobalPlayerCount").ReadStatic<int>();

// Static managed reference. The returned nint is the remote Game.Player object address.
nint playerAddress = gameStateType.ResolveField("CurrentPlayer").ReadStaticReference();

if (playerAddress == 0)
    throw new InvalidOperationException("Game.GameState.CurrentPlayer is null.");

// Static enum.
Game.PlayerState globalState = gameStateType.ResolveField("GlobalState").ReadStaticEnum<Game.PlayerState>();

// Static System.String.
string? serverName = gameStateType.ResolveField("ServerName").ReadStaticString();

// Static single-dimensional managed array.
ResolvedArray? players = gameStateType.ResolveField("Players").ReadStaticArray();

// Static blittable value type.
Game.Math.Position spawnPosition = gameStateType.ResolveField("SpawnPosition").ReadStaticBlittable<Game.Math.Position>();

// Instance scalar.
int health = playerType.ResolveField("Health").Read<int>(playerAddress);

// Instance managed reference.
nint targetAddress = playerType.ResolveField("Target").ReadReference(playerAddress);

// Instance enum.
Game.PlayerState state = playerType.ResolveField("State").ReadEnum<Game.PlayerState>(playerAddress);

// Instance System.String.
string? name = playerType.ResolveField("Name").ReadString(playerAddress);

// Instance single-dimensional managed array.
ResolvedArray? items = playerType.ResolveField("Items").ReadArray(playerAddress);

// Instance blittable value type.
Game.Math.Position position = playerType.ResolveField("Position").ReadBlittable<Game.Math.Position>(playerAddress);

Console.WriteLine($"Global players: {globalPlayerCount}");
Console.WriteLine($"Current player*: 0x{playerAddress:X}");
Console.WriteLine($"Global state: {globalState}");
Console.WriteLine($"Server: {serverName ?? "<null>"}");
Console.WriteLine($"Players array length: {players?.Length.ToString() ?? "<null>"}");
Console.WriteLine($"Spawn: {spawnPosition.X}, {spawnPosition.Y}, {spawnPosition.Z}");
Console.WriteLine($"Health: {health}");
Console.WriteLine($"Target*: 0x{targetAddress:X}");
Console.WriteLine($"State: {state}");
Console.WriteLine($"Name: {name ?? "<null>"}");
Console.WriteLine($"Items array length: {items?.Length.ToString() ?? "<null>"}");
Console.WriteLine($"Position: {position.X}, {position.Y}, {position.Z}");
```

The exact C# scalar type must match the IL2CPP runtime type. The matching enum and struct declarations used above are explained in [Map IL2CPP values to C#](#12-map-il2cpp-values-to-c).

## Field storage kinds

Use `StorageKind` before deciding how to read a field:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType inputSystem = resolver.ResolveType(new TypeQuery("Unity.InputSystem", "UnityEngine.InputSystem", "InputSystem"));

foreach (ResolvedField field in inputSystem.GetFields())
{
    switch (field.StorageKind)
    {
        case FieldStorageKind.Instance:
            Console.WriteLine($"{field.Query.Name}: instance offset 0x{field.InstanceOffset:X}");
            break;

        case FieldStorageKind.Static:
            Console.WriteLine($"{field.Query.Name}: static storage offset 0x{field.StaticStorageOffset:X}");
            break;

        case FieldStorageKind.ThreadStatic:
            Console.WriteLine($"{field.Query.Name}: thread-static storage is not readable through the normal field readers.");
            break;

        case FieldStorageKind.Literal:
            Console.WriteLine($"{field.Query.Name}: metadata literal; there is no ordinary mutable field storage.");
            break;
    }
}
```

---

# 9. Follow managed object references

A managed reference read returns an `nint` representing the remote `Il2CppObject*`.

That address is often the bridge from a static/global object to useful instance data.

The following real workflow starts from `InputSystem.s_Manager`, follows the reference, and uses the resulting object address as the receiver for instance fields:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;
using System.Linq;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType inputSystem = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

// Step 1: read a static managed reference.
ResolvedField managerField = inputSystem.ResolveField("s_Manager");
nint managerAddress = managerField.ReadStaticReference();

if (managerAddress == 0)
    throw new InvalidOperationException("InputSystem.s_Manager is null.");

// Step 2: resolve the semantic type of the referenced object.
ResolvedType inputManager = assembly.ResolveType("UnityEngine.InputSystem", "InputManager");

// Step 3: use the object address as the receiver for instance fields.
foreach (ResolvedField field in inputManager.GetFields().Where(field => field.StorageKind == FieldStorageKind.Instance))
    Console.WriteLine($"{field.TypeName} {field.Query.Name} @ +0x{field.InstanceOffset:X}");

// ReadReference also exists for instance fields when one object points to another.
ResolvedField? referenceField = inputManager.GetFields().FirstOrDefault(field =>
    field.StorageKind == FieldStorageKind.Instance &&
    !field.TypeName.StartsWith("System.", StringComparison.Ordinal) &&
    !field.TypeName.EndsWith("[]", StringComparison.Ordinal));

if (referenceField is not null)
{
    try
    {
        nint nestedObjectAddress = referenceField.ReadReference(managerAddress);
        Console.WriteLine($"{referenceField.Query.Name}*: 0x{nestedObjectAddress:X}");
    }
    catch (InvalidOperationException)
    {
        // The field may be a value type rather than a managed object reference.
        Console.WriteLine($"{referenceField.Query.Name} is not a supported managed reference category.");
    }
}
```

Do not dereference the returned `nint` directly in your process. Pass it back to resolver endpoints that expect an `instanceAddress`.

References can come from:

- `ResolvedField.ReadReference(instanceAddress)`;
- `ResolvedField.ReadStaticReference()`;
- `ResolvedProperty.ReadReference(instanceAddress)`;
- `ResolvedProperty.ReadStaticReference()`;
- `ResolvedArray.ReadReference(index)`.

A reference can legitimately be zero, which represents `null`. Always check for zero before using an address as an instance receiver when null is possible.

Property getters are execution results rather than field snapshots. Non-null object and array results returned by supported property getters are kept valid by the resolver for the current cache generation. `ClearCache()` or `Dispose()` ends that retained lifetime, so reacquire those references afterward.

---

# 10. Read properties

A property is different from a field. Reading a property executes its supported parameterless getter instead of reading a field storage location directly.

The current API supports parameterless static and instance getters. It does not invoke setters or indexed/parameterized getters.

## Static getter → object reference → instance getter

This is the most useful property workflow because it shows how multiple parts of the API compose:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType inputSystem = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

// pollingFrequency is a static scalar property.
ResolvedProperty pollingFrequency = inputSystem.ResolveProperty("pollingFrequency");

Console.WriteLine($"Can read: {pollingFrequency.CanRead}");
Console.WriteLine($"Can write: {pollingFrequency.CanWrite}");
Console.WriteLine($"Getter: {pollingFrequency.Getter?.Query.Name}");
Console.WriteLine($"Setter: {pollingFrequency.Setter?.Query.Name}");

float pollingFrequencyValue = pollingFrequency.ReadStatic<float>();
Console.WriteLine($"Polling frequency: {pollingFrequencyValue}");

// settings is a static property returning a managed InputSettings object.
ResolvedProperty settings = inputSystem.ResolveProperty("settings");
nint settingsAddress = settings.ReadStaticReference();

if (settingsAddress == 0)
    throw new InvalidOperationException("InputSystem.settings returned null.");

Console.WriteLine($"InputSettings*: 0x{settingsAddress:X}");

// Resolve the referenced object's type and invoke one of its instance getters.
ResolvedType inputSettings = assembly.ResolveType("UnityEngine.InputSystem", "InputSettings");
ResolvedProperty pressPoint = inputSettings.ResolveProperty("defaultButtonPressPoint");
float pressPointValue = pressPoint.Read<float>(settingsAddress);

Console.WriteLine($"Default button press point: {pressPointValue}");
```

`CanRead` means a getter exists. `CanWrite` means a setter exists in runtime metadata; it does **not** mean the current resolver can invoke that setter.

## Every property value category

Once navigation tells you the property's runtime type and whether its getter is static, choose the matching endpoint. The following representative model demonstrates every property-reading endpoint in one workflow:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Representative properties discovered through navigation:
// Assembly-CSharp
//   Game.GameState
//     static int GlobalPlayerCount { get; }
//     static Game.Player CurrentPlayer { get; }
//     static Game.PlayerState GlobalState { get; }
//     static string ServerName { get; }
//     static Game.Player[] Players { get; }
//     static Game.Math.Position SpawnPosition { get; }
//
//   Game.Player
//     int Level { get; }
//     Game.Player Target { get; }
//     Game.PlayerState State { get; }
//     string DisplayName { get; }
//     Game.Item[] Items { get; }
//     Game.Math.Position Position { get; }

ResolvedType gameStateType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "GameState"));
ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));

// Static scalar getter.
int globalPlayerCount = gameStateType.ResolveProperty("GlobalPlayerCount").ReadStatic<int>();

// Static reference getter.
nint playerAddress = gameStateType.ResolveProperty("CurrentPlayer").ReadStaticReference();

if (playerAddress == 0)
    throw new InvalidOperationException("Game.GameState.CurrentPlayer returned null.");

// Static enum getter.
Game.PlayerState globalState = gameStateType.ResolveProperty("GlobalState").ReadStaticEnum<Game.PlayerState>();

// Static string getter.
string? serverName = gameStateType.ResolveProperty("ServerName").ReadStaticString();

// Static array getter.
ResolvedArray? players = gameStateType.ResolveProperty("Players").ReadStaticArray();

// Static blittable value-type getter.
Game.Math.Position spawnPosition = gameStateType.ResolveProperty("SpawnPosition").ReadStaticBlittable<Game.Math.Position>();

// Instance scalar getter.
int level = playerType.ResolveProperty("Level").Read<int>(playerAddress);

// Instance reference getter.
nint targetAddress = playerType.ResolveProperty("Target").ReadReference(playerAddress);

// Instance enum getter.
Game.PlayerState state = playerType.ResolveProperty("State").ReadEnum<Game.PlayerState>(playerAddress);

// Instance string getter.
string? displayName = playerType.ResolveProperty("DisplayName").ReadString(playerAddress);

// Instance array getter.
ResolvedArray? items = playerType.ResolveProperty("Items").ReadArray(playerAddress);

// Instance blittable value-type getter.
Game.Math.Position position = playerType.ResolveProperty("Position").ReadBlittable<Game.Math.Position>(playerAddress);

Console.WriteLine($"Global players: {globalPlayerCount}");
Console.WriteLine($"Current player*: 0x{playerAddress:X}");
Console.WriteLine($"Global state: {globalState}");
Console.WriteLine($"Server: {serverName ?? "<null>"}");
Console.WriteLine($"Players array length: {players?.Length.ToString() ?? "<null>"}");
Console.WriteLine($"Spawn: {spawnPosition.X}, {spawnPosition.Y}, {spawnPosition.Z}");
Console.WriteLine($"Level: {level}");
Console.WriteLine($"Target*: 0x{targetAddress:X}");
Console.WriteLine($"State: {state}");
Console.WriteLine($"Name: {displayName ?? "<null>"}");
Console.WriteLine($"Items array length: {items?.Length.ToString() ?? "<null>"}");
Console.WriteLine($"Position: {position.X}, {position.Y}, {position.Z}");
```

The matching `Game.PlayerState` and `Game.Math.Position` declarations are shown in [Map IL2CPP values to C#](#12-map-il2cpp-values-to-c).

---

# 11. Read arrays

`ReadArray()` and `ReadStaticArray()` return a `ResolvedArray?` rather than copying an unknown remote array into a local C# array.

`ResolvedArray` tells you:

- the remote array address;
- its length;
- the semantic element type;
- how to read one validated element.

The following example starts from the real `InputManager` object and inspects an array field discovered at runtime:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;
using System.Linq;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType inputSystem = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");
nint managerAddress = inputSystem.ResolveField("s_Manager").ReadStaticReference();
ResolvedType inputManager = assembly.ResolveType("UnityEngine.InputSystem", "InputManager");

ResolvedField? arrayField = inputManager.GetFields().FirstOrDefault(field =>
    field.StorageKind == FieldStorageKind.Instance &&
    field.TypeName.EndsWith("[]", StringComparison.Ordinal));

if (arrayField is null)
{
    Console.WriteLine("This InputManager version does not expose an instance vector-array field.");
    return;
}

ResolvedArray? array = arrayField.ReadArray(managerAddress);

if (array is null)
{
    Console.WriteLine($"{arrayField.Query.Name} is null.");
    return;
}

Console.WriteLine($"Array*: 0x{array.Address:X}");
Console.WriteLine($"Length: {array.Length}");
Console.WriteLine($"Element type: {array.ElementTypeName}");

if (array.Length == 0)
    return;

// Pick the element reader that matches ElementTypeName.
if (array.ElementTypeName == "System.Int32")
{
    int value = array.Read<int>(0);
    Console.WriteLine(value);
}
else if (array.ElementTypeName == "System.String")
{
    string? value = array.ReadString(0);
    Console.WriteLine(value ?? "<null>");
}
else
{
    // Class/interface/object array elements can be read as managed references.
    // If the element is actually a value type, ReadReference will reject it.
    try
    {
        nint elementAddress = array.ReadReference(0);
        Console.WriteLine($"Element[0]*: 0x{elementAddress:X}");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("Element[0] is not a supported managed reference; use ReadEnum<TEnum> or ReadBlittable<T> when appropriate.");
    }
}
```

All element categories are explicit. This representative example demonstrates every `ResolvedArray` reader from resolver attachment to the final local value:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Representative static arrays discovered through navigation:
// Game.ArraySamples.Numbers   -> System.Int32[]
// Game.ArraySamples.States    -> Game.PlayerState[]
// Game.ArraySamples.Players   -> Game.Player[]
// Game.ArraySamples.Names     -> System.String[]
// Game.ArraySamples.Positions -> Game.Math.Position[]
ResolvedType samples = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "ArraySamples"));

ResolvedArray? numbers = samples.ResolveField("Numbers").ReadStaticArray();
ResolvedArray? states = samples.ResolveField("States").ReadStaticArray();
ResolvedArray? players = samples.ResolveField("Players").ReadStaticArray();
ResolvedArray? names = samples.ResolveField("Names").ReadStaticArray();
ResolvedArray? positions = samples.ResolveField("Positions").ReadStaticArray();

if (numbers is not null && numbers.Length > 0)
{
    int number = numbers.Read<int>(0);
    Console.WriteLine(number);
}

if (states is not null && states.Length > 0)
{
    Game.PlayerState state = states.ReadEnum<Game.PlayerState>(0);
    Console.WriteLine(state);
}

if (players is not null && players.Length > 0)
{
    nint objectAddress = players.ReadReference(0);
    Console.WriteLine($"Player[0]*: 0x{objectAddress:X}");
}

if (names is not null && names.Length > 0)
{
    string? text = names.ReadString(0);
    Console.WriteLine(text ?? "<null>");
}

if (positions is not null && positions.Length > 0)
{
    Game.Math.Position position = positions.ReadBlittable<Game.Math.Position>(0);
    Console.WriteLine($"{position.X}, {position.Y}, {position.Z}");
}
```

Indexes are zero-based. Passing an index below zero or greater than or equal to `Length` is rejected.

Only single-dimensional zero-based managed arrays (`T[]`) are currently supported. Multidimensional arrays are intentionally unsupported.

---

# 12. Map IL2CPP values to C#

The resolver does not treat every sequence of bytes as interchangeable. You choose a reader according to the runtime type discovered through navigation.

| Runtime value | Consumer API | C# representation |
| --- | --- | --- |
| Supported scalar | `Read<T>()` / `ReadStatic<T>()` | exact scalar `T` |
| Enum | `ReadEnum<TEnum>()` | matching C# enum |
| Class/interface/object reference | `ReadReference()` | remote address as `nint` |
| `System.String` | `ReadString()` | `string?` |
| `T[]` | `ReadArray()` | `ResolvedArray?` |
| Blittable non-enum value type | `ReadBlittable<T>()` | matching unmanaged C# struct |

## Supported scalar types

The scalar reader supports:

```text
System.Boolean  → bool
System.Char     → char
System.SByte    → sbyte
System.Byte     → byte
System.Int16    → short
System.UInt16   → ushort
System.Int32    → int
System.UInt32   → uint
System.Int64    → long
System.UInt64   → ulong
System.Single   → float
System.Double   → double
System.IntPtr   → nint
System.UIntPtr  → nuint
```

Example:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));
nint playerAddress = playerType.ResolveProperty("Current").ReadStaticReference();

bool alive = playerType.ResolveField("IsAlive").Read<bool>(playerAddress);
int health = playerType.ResolveField("Health").Read<int>(playerAddress);
float speed = playerType.ResolveField("Speed").Read<float>(playerAddress);

Console.WriteLine($"Alive={alive}, Health={health}, Speed={speed}");
```

## Matching C# enums

For an enum read, the C# enum must match the IL2CPP enum's semantic full name and underlying integral type.

Suppose live navigation shows:

```text
Game.PlayerState
underlying type: System.Int32
```

Declare the local enum with the same semantic name:

```csharp
// File: PlayerState.cs
namespace Game;

public enum PlayerState : int
{
    Idle = 0,
    Moving = 1,
    Fighting = 2
}
```

Then use it directly with fields, properties, or arrays:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));
nint playerAddress = playerType.ResolveProperty("Current").ReadStaticReference();

Game.PlayerState fieldState = playerType.ResolveField("State").ReadEnum<Game.PlayerState>(playerAddress);
Game.PlayerState propertyState = playerType.ResolveProperty("State").ReadEnum<Game.PlayerState>(playerAddress);
ResolvedArray? history = playerType.ResolveField("StateHistory").ReadArray(playerAddress);

if (history is not null && history.Length > 0)
{
    Game.PlayerState firstState = history.ReadEnum<Game.PlayerState>(0);
    Console.WriteLine(firstState);
}

Console.WriteLine(fieldState);
Console.WriteLine(propertyState);
```

The resolver validates the enum identity and underlying type. It does not compare your local enum member names and values one by one, so your declaration must still reproduce the target enum correctly if you want meaningful names.

## Matching C# blittable structs

This is the API used when an IL2CPP value type can be represented by a plain unmanaged C# struct.

Suppose navigation and `GetMetadata()` show a runtime type:

```text
Game.Math.Position
IsValueType = true
IsEnum      = false
IsBlittable = true
ValueSize   = 12
```

Create a matching local C# struct:

```csharp
// File: Position.cs
using System.Runtime.InteropServices;

namespace Game.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Position
{
    public float X;
    public float Y;
    public float Z;
}
```

Then validate the runtime metadata and read it as a normal C# value:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType positionType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game.Math", "Position"));
ResolvedTypeMetadata metadata = positionType.GetMetadata();

Console.WriteLine($"Value type: {metadata.IsValueType}");
Console.WriteLine($"Enum: {metadata.IsEnum}");
Console.WriteLine($"Blittable: {metadata.IsBlittable}");
Console.WriteLine($"Runtime size: {metadata.ValueSize}");
Console.WriteLine($"Runtime alignment: {metadata.ValueAlignment}");

ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));
nint playerAddress = playerType.ResolveProperty("Current").ReadStaticReference();

// Field value → local struct.
Game.Math.Position fieldPosition = playerType.ResolveField("Position").ReadBlittable<Game.Math.Position>(playerAddress);

// Property getter result → local struct.
Game.Math.Position propertyPosition = playerType.ResolveProperty("Position").ReadBlittable<Game.Math.Position>(playerAddress);

// Array element → local struct.
ResolvedArray? positions = playerType.ResolveField("PreviousPositions").ReadArray(playerAddress);

if (positions is not null && positions.Length > 0)
{
    Game.Math.Position firstPosition = positions.ReadBlittable<Game.Math.Position>(0);
    Console.WriteLine($"Array value: {firstPosition.X}, {firstPosition.Y}, {firstPosition.Z}");
}

Console.WriteLine($"Field value: {fieldPosition.X}, {fieldPosition.Y}, {fieldPosition.Z}");
Console.WriteLine($"Property value: {propertyPosition.X}, {propertyPosition.Y}, {propertyPosition.Z}");
```

For `ReadBlittable<T>()`, the resolver requires:

- `T` to be `unmanaged`;
- the runtime type to be a non-enum value type;
- the runtime to report the type as blittable;
- `sizeof(T)` to exactly match the IL2CPP value size;
- the semantic full name of `T` to match the IL2CPP type name.

Common nested-type separators are normalized when names are compared.

The resolver does **not** compare every member offset in your local struct against the runtime type. You remain responsible for declaring the local struct with the correct field order, field types, packing, and layout.

## Strings

Use the specialized string reader instead of treating `System.String` as a generic reference:

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));
nint playerAddress = playerType.ResolveProperty("Current").ReadStaticReference();

string? fieldName = playerType.ResolveField("Name").ReadString(playerAddress);
string? propertyName = playerType.ResolveProperty("DisplayName").ReadString(playerAddress);

Console.WriteLine(fieldName ?? "<null>");
Console.WriteLine(propertyName ?? "<null>");
```

`string?` is nullable because a managed string reference can be null.

---

# 13. Work with static-field storage and layouts

Most consumers can ignore structural layouts until they need native method-code resolution or the target cannot expose normal static-field storage through public runtime APIs.

A layout is a version-specific description of where a required value lives inside an IL2CPP native structure.

## Built-in layouts, global selection, reset and custom candidates

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Built-in profiles can be accessed directly.
Il2CppMethodInfoLayout directMethodPointerFirst = Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64;
Il2CppClassLayout class29_1 = Il2CppClassLayouts.Class29_1X64;
Il2CppClassLayout class29_2 = Il2CppClassLayouts.Class29_2X64;

Console.WriteLine(directMethodPointerFirst.Name);
Console.WriteLine(class29_1.Name);
Console.WriteLine(class29_2.Name);

// Or enumerate every built-in profile available in the current resolver version.
foreach (Il2CppMethodInfoLayout layout in Il2CppMethodInfoLayouts.BuiltIn)
    Console.WriteLine($"{layout.Name}: method pointer +0x{layout.DirectMethodPointerOffset:X}");

foreach (Il2CppClassLayout layout in Il2CppClassLayouts.BuiltIn)
    Console.WriteLine($"{layout.Name}: static_fields +0x{layout.StaticFieldsPointerOffset:X}, size +0x{layout.StaticFieldsSizeOffset:X}");

// Select layouts globally for subsequent default calls.
resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

Console.WriteLine(resolver.MethodInfoLayout?.Name);
Console.WriteLine(resolver.FieldStorageLayout?.Name);

// Consumers can define custom immutable layouts when supporting another target profile.
Il2CppMethodInfoLayout customMethodLayout = new(
    "CustomMethodInfo",
    directMethodPointerOffset: 0x0);

Il2CppClassLayout customClassLayout = new(
    "CustomIl2CppClass",
    staticFieldsPointerOffset: 0xB8,
    staticFieldsSizeOffset: 0x10C);

Console.WriteLine(customMethodLayout.Name);
Console.WriteLine(customMethodLayout.DirectMethodPointerOffset);
Console.WriteLine(customClassLayout.Name);
Console.WriteLine(customClassLayout.StaticFieldsPointerOffset);
Console.WriteLine(customClassLayout.StaticFieldsSizeOffset);

// Register adds a candidate for future automatic detection without selecting it globally.
bool methodCandidateAdded = resolver.RegisterMethodInfoLayout(customMethodLayout);
bool classCandidateAdded = resolver.RegisterFieldStorageLayout(customClassLayout);

Console.WriteLine($"Method candidate added: {methodCandidateAdded}");
Console.WriteLine($"Class candidate added: {classCandidateAdded}");

// Reset removes only the explicit global selection.
resolver.ResetMethodInfoLayout();
resolver.ResetFieldStorageLayout();

Console.WriteLine(resolver.MethodInfoLayout is null);
Console.WriteLine(resolver.FieldStorageLayout is null);
```

## Resolve normal static-field storage

You usually do not need `ResolvedFieldStorage` merely to read a supported static field; `ReadStatic*()` already performs the required storage resolution. Use storage resolution when you explicitly need the concrete static data block and computed field address.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

FieldQuery managerQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");

ResolvedField managerField = resolver.ResolveField(managerQuery);

// Default resolution uses the session's configured/runtime/detected path.
// On targets exposing the public runtime static-storage APIs, no structural class layout is required.
ResolvedFieldStorage defaultStorage = managerField.ResolveStorage();
ResolvedFieldStorage sameDefaultStorage = resolver.ResolveFieldStorage(managerQuery);

Console.WriteLine($"Field: {defaultStorage.Field.Query.Name}");
Console.WriteLine($"Static block*: 0x{defaultStorage.StaticFieldsAddress:X}");
Console.WriteLine($"Static block size: 0x{defaultStorage.StaticFieldsSize:X}");
Console.WriteLine($"Field offset: 0x{defaultStorage.StaticStorageOffset:X}");
Console.WriteLine($"Field address: 0x{defaultStorage.StorageAddress:X}");
Console.WriteLine($"Resolution source: {defaultStorage.ResolutionSource}");

if (defaultStorage.ResolutionSource == FieldStorageResolutionSource.RuntimeApi)
    Console.WriteLine("The target runtime provided the static storage directly.");
else if (defaultStorage.ResolutionSource == FieldStorageResolutionSource.ClassLayout)
    Console.WriteLine("A configured or detected Il2CppClass layout was used.");

Console.WriteLine($"Compatibility profile: {defaultStorage.CompatibilityProfile ?? "<runtime API>"}");
Console.WriteLine($"static_fields pointer offset: {defaultStorage.StaticFieldsPointerOffset?.ToString() ?? "<not used>"}");
Console.WriteLine($"static_fields_size offset: {defaultStorage.StaticFieldsSizeOffset?.ToString() ?? "<not used>"}");

// One-shot explicit layout: applies only to this operation.
ResolvedFieldStorage explicitStorage = managerField.ResolveStorage(Il2CppClassLayouts.Class29_2X64);
ResolvedFieldStorage explicitStorageFromResolver = resolver.ResolveFieldStorage(managerQuery, Il2CppClassLayouts.Class29_2X64);

Console.WriteLine(explicitStorage.ResolutionSource == FieldStorageResolutionSource.ClassLayout);
Console.WriteLine(explicitStorage.StorageAddress == explicitStorageFromResolver.StorageAddress);
```

`FieldStorageResolutionSource.RuntimeApi` means the resolver obtained the static block through runtime APIs. `FieldStorageResolutionSource.ClassLayout` means a structural `Il2CppClassLayout` was used.

## Explicit automatic class-layout detection with evidence

When runtime static-storage APIs are unavailable and no layout is known, the resolver can test registered class-layout candidates against static fields from multiple declaring classes.

You provide the evidence because the resolver should not guess which unrelated fields are trustworthy examples.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Optional: add custom candidates before detection.
resolver.RegisterFieldStorageLayout(new Il2CppClassLayout(
    "CandidateProfile",
    staticFieldsPointerOffset: 0xB8,
    staticFieldsSizeOffset: 0x10C));

FieldQuery target = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");

// Replace these representative queries with normal static fields you discovered
// on different declaring classes in your own target.
FieldQuery[] evidence =
{
    new("Assembly-CSharp", "Game", "GameManager", "Instance"),
    new("Assembly-CSharp", "Game", "PlayerManager", "LocalPlayer"),
    new("Assembly-CSharp", "Game", "WorldManager", "CurrentWorld")
};

ResolvedFieldStorage storage = resolver.ResolveFieldStorage(target, evidence);
Console.WriteLine($"Detected profile: {storage.CompatibilityProfile}");
Console.WriteLine($"Storage address: 0x{storage.StorageAddress:X}");
```

The detected layout is reused by later default storage operations until automatic evidence is invalidated by `ClearCache()` or candidate registration changes the detection context.

## One-shot layout overloads for static reads

Every static field reader has an overload that accepts an `Il2CppClassLayout`. This is useful when you want to force one compatibility profile for one read without changing `FieldStorageLayout` globally.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

Il2CppClassLayout layout = Il2CppClassLayouts.Class29_2X64;
ResolvedType sample = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "GameState"));

// Use each endpoint only with a field whose runtime type matches the requested category.
int count = sample.ResolveField("GlobalCount").ReadStatic<int>(layout);
nint current = sample.ResolveField("Current").ReadStaticReference(layout);
Game.PlayerState state = sample.ResolveField("State").ReadStaticEnum<Game.PlayerState>(layout);
string? name = sample.ResolveField("Name").ReadStaticString(layout);
ResolvedArray? players = sample.ResolveField("Players").ReadStaticArray(layout);
Game.Math.Position spawn = sample.ResolveField("SpawnPosition").ReadStaticBlittable<Game.Math.Position>(layout);

Console.WriteLine(count);
Console.WriteLine($"Current*: 0x{current:X}");
Console.WriteLine(state);
Console.WriteLine(name ?? "<null>");
Console.WriteLine(players?.Length ?? 0);
Console.WriteLine($"{spawn.X}, {spawn.Y}, {spawn.Z}");
```

The same methods without the `layout` argument use the resolver's normal storage-selection path.

---

# 14. Cache, identity and generations

Within one cache generation, the resolver preserves runtime identity. Resolving the same runtime entity again returns the same `Resolved*` object when possible.

This is useful because you can safely compare object references instead of manually comparing native addresses.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

TypeQuery typeQuery = new("Unity.InputSystem", "UnityEngine.InputSystem", "InputSystem");
MethodQuery methodQuery = new(typeQuery, "QueueEvent", "UnityEngine.InputSystem.LowLevel.InputEventPtr");
PropertyQuery propertyQuery = new(typeQuery, "pollingFrequency");
FieldQuery fieldQuery = new(typeQuery, "s_Manager");

ResolvedAssembly firstAssembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedAssembly secondAssembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType firstType = resolver.ResolveType(typeQuery);
ResolvedType secondType = resolver.ResolveType(typeQuery);
ResolvedMethod firstMethod = resolver.ResolveMethod(methodQuery);
ResolvedMethod secondMethod = resolver.ResolveMethod(methodQuery);
ResolvedProperty firstProperty = resolver.ResolveProperty(propertyQuery);
ResolvedProperty secondProperty = resolver.ResolveProperty(propertyQuery);
ResolvedField firstField = resolver.ResolveField(fieldQuery);
ResolvedField secondField = resolver.ResolveField(fieldQuery);

Console.WriteLine(ReferenceEquals(firstAssembly, secondAssembly)); // True
Console.WriteLine(ReferenceEquals(firstType, secondType));         // True
Console.WriteLine(ReferenceEquals(firstMethod, secondMethod));     // True
Console.WriteLine(ReferenceEquals(firstProperty, secondProperty)); // True
Console.WriteLine(ReferenceEquals(firstField, secondField));       // True

// Explicit layout selections survive ClearCache().
resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

resolver.ClearCache();

Console.WriteLine(resolver.MethodInfoLayout?.Name);
Console.WriteLine(resolver.FieldStorageLayout?.Name);

// Old objects remain readable snapshots, but navigation through them is invalidated.
Console.WriteLine(firstType.Query.Name); // Snapshot data is still available.

try
{
    firstType.GetFields();
}
catch (InvalidOperationException exception)
{
    Console.WriteLine($"Old generation rejected: {exception.Message}");
}

// Reacquire current-generation objects after clearing the cache.
ResolvedType refreshedType = resolver.ResolveType(typeQuery);
Console.WriteLine(ReferenceEquals(firstType, refreshedType)); // False
Console.WriteLine(refreshedType.GetFields().Count);
```

`ClearCache()` is useful when you deliberately want to discard cached runtime snapshots and reacquire current identities. Do not keep navigating old `Resolved*` objects after calling it.

---

# 15. Errors and common mistakes

The API fails explicitly when a request is semantically invalid, unsupported by the target, stale, or unsafe to interpret.

Common exception categories include:

| Exception | Typical meaning |
| --- | --- |
| `ArgumentException` / `ArgumentOutOfRangeException` | Invalid query input, address, index, layout, or timeout |
| `KeyNotFoundException` | Requested assembly/type/member was not found |
| `InvalidOperationException` | Wrong read category, wrong static/instance usage, incompatible type, stale generation, or invalid resolver state |
| `NotSupportedException` | Feature/type/runtime capability intentionally unsupported |
| `InvalidDataException` | Runtime data was inconsistent with the requested interpretation |
| `ObjectDisposedException` | The resolver/session has already been disposed |
| `TimeoutException` | A remote runtime operation exceeded its configured timeout |
| `Il2CppInvocationException` | A supported property getter threw a managed exception in the target |

## Managed getter exceptions

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Invocation;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

int processId = Process.GetProcessesByName("Dofus")[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

ResolvedType playerType = resolver.ResolveType(new TypeQuery("Assembly-CSharp", "Game", "Player"));
nint playerAddress = playerType.ResolveProperty("Current").ReadStaticReference();
ResolvedProperty riskyProperty = playerType.ResolveProperty("ComputedValue");

try
{
    int value = riskyProperty.Read<int>(playerAddress);
    Console.WriteLine(value);
}
catch (Il2CppInvocationException exception)
{
    // ExceptionAddress is the remote Il2CppException* reported by the runtime.
    // It is diagnostic only and is not retained for later dereferencing.
    Console.WriteLine(exception.Message);
    Console.WriteLine($"Remote exception*: 0x{exception.ExceptionAddress:X}");
}
```

## Common mistakes

### Using the wrong scalar type

If a field is `System.Int32`, use `Read<int>()`, not `Read<uint>()` merely because both occupy four bytes.

### Reading a static member as an instance member

Use `ReadStatic*()` for static fields/properties and instance overloads only for members requiring an object address.

### Passing a random address as an instance

Obtain object addresses from validated reference endpoints such as `ReadReference()`, `ReadStaticReference()`, or `ResolvedArray.ReadReference()` whenever possible.

### Using `ReadBlittable<T>()` for primitives or enums

Use scalar readers for primitives and enum readers for enums. `ReadBlittable<T>()` is deliberately reserved for matching non-enum value types.

### Assuming `CanWrite` means setters are supported

`CanWrite` describes metadata. Setter invocation is not implemented in the current resolver.

### Continuing to navigate after `ClearCache()`

Re-resolve the entities you need after clearing the cache.

---

# 16. Complete exploration workflow

This example is intentionally longer. It demonstrates how somebody with **only a running game and no dump** can move from process discovery to meaningful runtime inspection.

```csharp
using System.Diagnostics;
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Il2Cpp.Queries;
using System.Linq;

// 1. Find the running game.
Process[] processes = Process.GetProcessesByName("Dofus");

if (processes.Length == 0)
    throw new InvalidOperationException("Dofus is not running.");

int processId = processes[0].Id;
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);
Console.WriteLine($"Attached to PID {resolver.ProcessId}");

// 2. Explore assemblies. No dump is required.
IReadOnlyList<ResolvedAssembly> assemblies = resolver.GetAssemblies();
Console.WriteLine($"Loaded assemblies: {assemblies.Count}");

foreach (ResolvedAssembly assembly in assemblies.Where(assembly =>
    assembly.Name.Contains("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ||
    assembly.Name.Contains("InputSystem", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine(assembly.Name);
}

// 3. Resolve one assembly we know exists on this target.
ResolvedAssembly inputAssembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));

// 4. Browse its types to discover interesting names.
IReadOnlyList<ResolvedType> inputTypes = inputAssembly.GetTypes();

foreach (ResolvedType type in inputTypes.Where(type =>
    type.Query.Name.Contains("Input", StringComparison.OrdinalIgnoreCase)).Take(20))
{
    Console.WriteLine($"{type.Query.Namespace}.{type.Query.Name}");
}

// 5. Navigate a known type and inspect all member categories.
ResolvedType inputSystem = inputAssembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

Console.WriteLine("\nInputSystem fields:");
foreach (ResolvedField field in inputSystem.GetFields())
    Console.WriteLine($"{field.TypeName} {field.Query.Name} [{field.StorageKind}]");

Console.WriteLine("\nInputSystem properties:");
foreach (ResolvedProperty property in inputSystem.GetProperties())
    Console.WriteLine($"{property.TypeName} {property.Query.Name} (read={property.CanRead})");

Console.WriteLine("\nQueueEvent overloads:");
foreach (ResolvedMethod method in inputSystem.GetMethods("QueueEvent"))
    Console.WriteLine($"{method.ReturnTypeName} QueueEvent({string.Join(", ", method.ParameterTypeNames)})");

// 6. Read a static scalar property.
ResolvedProperty pollingFrequency = inputSystem.ResolveProperty("pollingFrequency");
float pollingFrequencyValue = pollingFrequency.ReadStatic<float>();
Console.WriteLine($"Polling frequency: {pollingFrequencyValue}");

// 7. Follow a property reference to another managed object.
ResolvedProperty settings = inputSystem.ResolveProperty("settings");
nint settingsAddress = settings.ReadStaticReference();
Console.WriteLine($"InputSettings*: 0x{settingsAddress:X}");

ResolvedType inputSettings = inputAssembly.ResolveType("UnityEngine.InputSystem", "InputSettings");
float pressPoint = inputSettings.ResolveProperty("defaultButtonPressPoint").Read<float>(settingsAddress);
Console.WriteLine($"Default button press point: {pressPoint}");

// 8. Follow a static field reference to InputManager and inspect its instance fields.
// The target used to validate the resolver uses Class29_2X64. Replace this if needed.
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

ResolvedField managerField = inputSystem.ResolveField("s_Manager");
nint managerAddress = managerField.ReadStaticReference();
Console.WriteLine($"InputManager*: 0x{managerAddress:X}");

ResolvedType inputManager = inputAssembly.ResolveType("UnityEngine.InputSystem", "InputManager");

foreach (ResolvedField field in inputManager.GetFields().Where(field => field.StorageKind == FieldStorageKind.Instance).Take(20))
    Console.WriteLine($"+0x{field.InstanceOffset:X} {field.TypeName} {field.Query.Name}");

// 9. Inspect type metadata to understand what you are looking at.
ResolvedType inputEventPtr = inputAssembly.ResolveType("UnityEngine.InputSystem.LowLevel", "InputEventPtr");
ResolvedTypeMetadata metadata = inputEventPtr.GetMetadata();

Console.WriteLine($"InputEventPtr value type: {metadata.IsValueType}");
Console.WriteLine($"InputEventPtr blittable: {metadata.IsBlittable}");
Console.WriteLine($"InputEventPtr size: {metadata.ValueSize}");
Console.WriteLine($"InputEventPtr alignment: {metadata.ValueAlignment}");

// 10. Resolve an exact method overload and map it to native code.
resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

ResolvedMethod queueEvent = inputSystem.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

ResolvedMethodCode code = queueEvent.ResolveCode();
Console.WriteLine($"QueueEvent MethodInfo*: 0x{queueEvent.MethodInfoAddress:X}");
Console.WriteLine($"QueueEvent native code: 0x{code.NativeAddress:X}");
Console.WriteLine($"Section: {code.SectionName}");
```

The important part is not the specific `Unity.InputSystem` names. The same navigation pattern applies to the game's own assemblies and types.

---

# 17. API coverage index

This section is a quick reference after you have learned the workflows above. Every supported consumer endpoint is grouped by the object that exposes it.

## `Il2CppResolver`

| Endpoint | Use |
| --- | --- |
| `Attach(int processId)` | Attach with the default per-call timeout |
| `Attach(int processId, TimeSpan callTimeout)` | Attach with an explicit per-call timeout |
| `ProcessId` | Read the owned target PID |
| `MethodInfoLayout` | Inspect the explicitly selected MethodInfo layout, if any |
| `FieldStorageLayout` | Inspect the explicitly selected Il2CppClass layout, if any |
| `SetMethodInfoLayout(...)` | Select a MethodInfo layout globally for the session |
| `SetFieldStorageLayout(...)` | Select a static-field class layout globally |
| `ResetMethodInfoLayout()` | Restore automatic MethodInfo layout selection |
| `ResetFieldStorageLayout()` | Restore runtime/detected static-field storage selection |
| `RegisterMethodInfoLayout(...)` | Add an automatic-detection candidate |
| `RegisterFieldStorageLayout(...)` | Add a class-layout detection candidate |
| `GetAssemblies()` | Enumerate loaded assemblies |
| `ResolveAssembly(AssemblyQuery)` | Resolve one known assembly |
| `ResolveType(TypeQuery)` | Resolve one known type |
| `ResolveMethod(MethodQuery)` | Resolve one exact method overload |
| `ResolveMethodCode(MethodQuery)` | Resolve method + native code with normal layout selection |
| `ResolveMethodCode(MethodQuery, layout)` | Resolve method + native code with a one-shot layout |
| `ResolveProperty(PropertyQuery)` | Resolve one property/indexer identity |
| `ResolveField(FieldQuery)` | Resolve one field |
| `ResolveFieldStorage(FieldQuery)` | Resolve normal static-field storage with normal selection |
| `ResolveFieldStorage(FieldQuery, layout)` | Resolve storage through one explicit layout |
| `ResolveFieldStorage(FieldQuery, evidence)` | Request class-layout detection from evidence fields |
| `ClearCache()` | Start a new cache generation |
| `Dispose()` | Release the resolver session |

## Queries

### `AssemblyQuery`

- `AssemblyQuery(string name)`
- `Name`

### `TypeQuery`

- `TypeQuery(string assemblyName, string namespaceName, string typeName)`
- `Assembly`
- `Namespace`
- `Name`

### `MethodQuery`

- `MethodQuery(string assemblyName, string namespaceName, string typeName, string methodName, params string[] parameterTypeNames)`
- `MethodQuery(TypeQuery declaringType, string methodName, params string[] parameterTypeNames)`
- `DeclaringType`
- `Name`
- `ParameterTypeNames`

### `PropertyQuery`

- `PropertyQuery(string assemblyName, string namespaceName, string typeName, string propertyName, params string[] indexParameterTypeNames)`
- `PropertyQuery(TypeQuery declaringType, string propertyName, params string[] indexParameterTypeNames)`
- `DeclaringType`
- `Name`
- `IndexParameterTypeNames`

### `FieldQuery`

- `FieldQuery(string assemblyName, string namespaceName, string typeName, string fieldName)`
- `FieldQuery(TypeQuery declaringType, string fieldName)`
- `DeclaringType`
- `Name`

## Layouts

### `Il2CppMethodInfoLayout`

- `Il2CppMethodInfoLayout(string name, int directMethodPointerOffset)`
- `Name`
- `DirectMethodPointerOffset`

### `Il2CppMethodInfoLayouts`

- `DirectMethodPointerFirstX64`
- `BuiltIn`

### `Il2CppClassLayout`

- `Il2CppClassLayout(string name, int staticFieldsPointerOffset, int staticFieldsSizeOffset)`
- `Name`
- `StaticFieldsPointerOffset`
- `StaticFieldsSizeOffset`

### `Il2CppClassLayouts`

- `Class29_1X64`
- `Class29_2X64`
- `BuiltIn`

## `ResolvedAssembly`

- `Query`
- `Name`
- `AssemblyAddress`
- `ImageAddress`
- `GetTypes()`
- `ResolveType(string namespaceName, string typeName)`

## `ResolvedType`

- `Query`
- `Assembly`
- `ClassAddress`
- `GetMethods()`
- `GetMethods(string name)`
- `ResolveMethod(string methodName, params string[] parameterTypeNames)`
- `GetProperties()`
- `GetProperties(string name)`
- `ResolveProperty(string propertyName, params string[] indexParameterTypeNames)`
- `GetFields()`
- `ResolveField(string fieldName)`
- `GetBaseType()`
- `GetInterfaces()`
- `GetNestedTypes()`
- `GetDeclaringType()`
- `GetMetadata()`

## `ResolvedTypeMetadata`

- `Attributes`
- `MetadataToken`
- `IsValueType`
- `IsEnum`
- `IsBlittable`
- `IsInterface`
- `IsAbstract`
- `IsSealed`
- `IsGeneric`
- `IsInflated`
- `ValueSize`
- `ValueAlignment`

## `ResolvedMethod`

- `Query`
- `DeclaringType`
- `MethodInfoAddress`
- `ReturnTypeName`
- `ParameterTypeNames`
- `GetMetadata()`
- `ResolveCode()`
- `ResolveCode(Il2CppMethodInfoLayout layout)`

## `ResolvedMethodMetadata`

- `Attributes`
- `ImplementationAttributes`
- `MetadataToken`
- `IsStatic`
- `IsInstance`
- `IsPublic`
- `IsVirtual`
- `IsAbstract`
- `IsFinal`
- `IsGeneric`
- `IsInflated`

## `ResolvedMethodCode`

- `Method`
- `NativeAddress`
- `SectionName`
- `CompatibilityProfile`
- `MethodPointerOffset`

## `ResolvedProperty`

- `Query`
- `DeclaringType`
- `PropertyInfoAddress`
- `TypeName`
- `IndexParameterTypeNames`
- `Attributes`
- `Getter`
- `Setter`
- `CanRead`
- `CanWrite`
- `Read<T>(nint instanceAddress)`
- `ReadStatic<T>()`
- `ReadEnum<TEnum>(nint instanceAddress)`
- `ReadStaticEnum<TEnum>()`
- `ReadReference(nint instanceAddress)`
- `ReadStaticReference()`
- `ReadString(nint instanceAddress)`
- `ReadStaticString()`
- `ReadArray(nint instanceAddress)`
- `ReadStaticArray()`
- `ReadBlittable<T>(nint instanceAddress)`
- `ReadStaticBlittable<T>()`

## `ResolvedField`

- `Query`
- `DeclaringType`
- `FieldInfoAddress`
- `TypeName`
- `Attributes`
- `StorageKind`
- `InstanceOffset`
- `StaticStorageOffset`
- `ResolveStorage()`
- `ResolveStorage(Il2CppClassLayout layout)`
- `ReadStatic<T>()`
- `ReadStatic<T>(Il2CppClassLayout layout)`
- `ReadStaticReference()`
- `ReadStaticReference(Il2CppClassLayout layout)`
- `ReadStaticEnum<TEnum>()`
- `ReadStaticEnum<TEnum>(Il2CppClassLayout layout)`
- `ReadStaticString()`
- `ReadStaticString(Il2CppClassLayout layout)`
- `ReadStaticArray()`
- `ReadStaticArray(Il2CppClassLayout layout)`
- `ReadStaticBlittable<T>()`
- `ReadStaticBlittable<T>(Il2CppClassLayout layout)`
- `Read<T>(nint instanceAddress)`
- `ReadReference(nint instanceAddress)`
- `ReadEnum<TEnum>(nint instanceAddress)`
- `ReadString(nint instanceAddress)`
- `ReadArray(nint instanceAddress)`
- `ReadBlittable<T>(nint instanceAddress)`

## `ResolvedFieldStorage`

- `Field`
- `StaticFieldsAddress`
- `StaticFieldsSize`
- `StaticStorageOffset`
- `StorageAddress`
- `ResolutionSource`
- `CompatibilityProfile`
- `StaticFieldsPointerOffset`
- `StaticFieldsSizeOffset`

## `FieldStorageKind`

- `Instance`
- `Static`
- `ThreadStatic`
- `Literal`

## `FieldStorageResolutionSource`

- `RuntimeApi`
- `ClassLayout`

## `ResolvedArray`

- `Address`
- `Length`
- `ElementTypeName`
- `Read<T>(int index)`
- `ReadEnum<TEnum>(int index)`
- `ReadReference(int index)`
- `ReadString(int index)`
- `ReadBlittable<T>(int index)`

## `Il2CppInvocationException`

- `Message` inherited from `Exception`
- `ExceptionAddress`

---

# 18. Current limitations

The current consumer API intentionally does not provide:

- general `ResolvedMethod.Invoke(...)` support;
- invocation of methods with arbitrary arguments;
- property setter invocation;
- indexed/parameterized property getter invocation;
- generic getter invocation;
- thread-static field storage reads;
- literal value extraction through the normal field-storage API;
- multidimensional managed arrays;
- arbitrary non-blittable value-type reconstruction;
- automatic local C# class proxies for remote managed objects;
- writes to game fields or properties;
- object construction.

Some navigation and read features also depend on optional exports exposed by the target's IL2CPP runtime. When a required capability is unavailable, the affected endpoint fails explicitly rather than silently applying an unsafe interpretation.

---

# 19. Technical documentation

This README deliberately avoids implementation-level details so it can remain a linear guide for consumers who simply want to explore and read a running game.

For maintainers and advanced contributors, [tech-doc.md](tech-doc.md) documents the internal architecture, runtime model, remote execution, layout detection, cache implementation, lifetime rules, safety model, and validation strategy.
