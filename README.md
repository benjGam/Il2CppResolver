# Unity IL2CPP Resolver

Semantic IL2CPP resolution and runtime navigation for live Windows x64 Unity processes.

```text
Assembly → Type → Method   → Native Code / Metadata
        │       ├→ Property → Getter / Setter Methods
        │       └→ Field    → Static Storage → Validated Value Read
        └────────→ Base / Interfaces / Nested / Declaring / Metadata
```

`Il2CppResolver` resolves managed identities such as assemblies, types, methods, properties, and fields against a live IL2CPP runtime. It keeps semantic resolution separate from version-sensitive native layout interpretation, and exposes session-bound navigation endpoints for exploring the resolved runtime without leaking the internal process, PE, remote-call, or cache infrastructure.

The current implementation is runtime-backed: it discovers and calls exported IL2CPP APIs from `GameAssembly.dll`, caches the resulting runtime metadata locally for the lifetime of the resolver session, and uses explicit compatibility layouts only where public runtime APIs are unavailable or the consumer deliberately selects a structural path.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Public Namespaces](#public-namespaces)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [Semantic Queries](#semantic-queries)
  - [Resolved Runtime Entities](#resolved-runtime-entities)
  - [Native Mapping Results](#native-mapping-results)
- [Resolver Lifetime](#resolver-lifetime)
- [Layout Configuration](#layout-configuration)
  - [Built-in MethodInfo Layouts](#built-in-methodinfo-layouts)
  - [Built-in Il2CppClass Layouts](#built-in-il2cppclass-layouts)
  - [Global Layout Selection](#global-layout-selection)
  - [Resetting Global Layout Selection](#resetting-global-layout-selection)
  - [One-shot Layout Overrides](#one-shot-layout-overrides)
  - [Custom Layouts](#custom-layouts)
  - [Registering Automatic-detection Candidates](#registering-automatic-detection-candidates)
- [Automatic Layout Detection](#automatic-layout-detection)
  - [MethodInfo Detection](#methodinfo-detection)
  - [Il2CppClass Detection for Field Storage](#il2cppclass-detection-for-field-storage)
- [Assemblies](#assemblies)
  - [Enumerating Assemblies](#enumerating-assemblies)
  - [Targeted Assembly Resolution](#targeted-assembly-resolution)
- [Types](#types)
  - [Targeted Type Resolution](#targeted-type-resolution)
  - [Enumerating Types](#enumerating-types)
  - [Type Relationships](#type-relationships)
  - [Type Metadata](#type-metadata)
- [Methods](#methods)
  - [Exact Method Resolution](#exact-method-resolution)
  - [Enumerating Methods by Name](#enumerating-methods-by-name)
  - [Enumerating All Methods](#enumerating-all-methods)
  - [Method Metadata](#method-metadata)
- [Native Method Code](#native-method-code)
- [Properties](#properties)
  - [Exact Property Resolution](#exact-property-resolution)
  - [Enumerating Properties by Name](#enumerating-properties-by-name)
  - [Enumerating All Properties](#enumerating-all-properties)
  - [Property Accessors](#property-accessors)
  - [Property Value Reading](#property-value-reading)
- [Fields](#fields)
  - [Targeted Field Resolution](#targeted-field-resolution)
  - [Enumerating Fields](#enumerating-fields)
  - [Field Storage Kinds](#field-storage-kinds)
  - [Field Value Reading](#field-value-reading)
- [Static Field Storage](#static-field-storage)
  - [Default Static-storage Selection Order](#default-static-storage-selection-order)
  - [Resolution Source](#resolution-source)
- [Navigation API](#navigation-api)
- [Caching and Runtime Catalogues](#caching-and-runtime-catalogues)
- [Targeted Resolution vs. Enumeration](#targeted-resolution-vs-enumeration)
- [Runtime Identity Map](#runtime-identity-map)
- [ClearCache and Cache Generations](#clearcache-and-cache-generations)
  - [Generation Invalidation](#generation-invalidation)
- [Concurrency Model](#concurrency-model)
- [Error Handling](#error-handling)
- [Performance Characteristics](#performance-characteristics)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Runtime Execution Model](#runtime-execution-model)
- [Safety Model](#safety-model)
  - [Remote timeout safety](#remote-timeout-safety)
- [Required and Optional Runtime Capabilities](#required-and-optional-runtime-capabilities)
  - [Optional type enumeration](#optional-type-enumeration)
  - [Optional property navigation](#optional-property-navigation)
  - [Optional property getter invocation](#optional-property-getter-invocation)
  - [Optional runtime static-field storage](#optional-runtime-static-field-storage)
  - [Optional field-value inspection](#optional-field-value-inspection)
  - [Optional type relationships and metadata](#optional-type-relationships-and-metadata)
- [Current Limitations](#current-limitations)
- [Complete Example](#complete-example)
- [Development and Validation](#development-and-validation)
- [Roadmap](#roadmap)

---

## Features

- Attach to a live Windows x64 IL2CPP process.
- Discover and validate `GameAssembly.dll` and required IL2CPP exports.
- Enumerate loaded assemblies.
- Resolve assemblies by semantic name.
- Resolve types by assembly, namespace, and managed type name.
- Enumerate image types when the target exposes the optional image/class enumeration APIs.
- Resolve exact method overloads by ordered semantic parameter-type names.
- Enumerate all methods or only overloads sharing one method name.
- Resolve properties by exact name and ordered index-parameter type names.
- Enumerate properties and reuse getter/setter methods through the runtime identity map.
- Invoke parameterless property getters through the supported IL2CPP runtime API with explicit thread attachment, managed-exception capture, virtual dispatch, and strong GC rooting of returned objects.
- Resolve fields and classify their runtime storage kind.
- Read explicitly supported scalar, enum, managed-reference, managed-string, vector-array, and blittable value-type field values with runtime type and storage-bound validation.
- Navigate parent types, interfaces, nested types, and declaring types through session-bound resolved objects.
- Inspect cached type and method metadata including attributes, tokens, generic state, value-type size/alignment, and method implementation flags.
- Map `MethodInfo*` to validated executable native code through explicit or automatically detected layouts.
- Resolve normal static-field storage through the public IL2CPP runtime API when available.
- Fall back to explicit or detected `Il2CppClass` layouts for static-field storage.
- Register custom layout candidates for automatic detection.
- Configure layouts globally for one resolver session.
- Override a layout for one operation without modifying session configuration.
- Reuse session-scoped runtime catalogues, semantic caches, and runtime identity maps.
- Invalidate stale navigable entities safely through cache generations.
- Serialize resolver operations per session.
- Execute only short-lived remote calls with finite timeouts and defensive cleanup rules.

---

## Requirements

The current implementation requires:

- Windows.
- A 64-bit resolver host process.
- A native Windows x64 target process.
- A Unity IL2CPP target with an accessible `GameAssembly.dll`.
- The IL2CPP exports required by the semantic runtime backend.
- .NET 8.

The project currently builds as an executable during development. The resolver API itself is exposed through the `UnityIl2CppResolver.Il2Cpp` namespace and is designed to be consumed independently from the native implementation details.

Some features are capability-based rather than attachment requirements:

- `ResolvedAssembly.GetTypes()` requires the optional image/class enumeration exports.
- Property navigation and `ResolveProperty(...)` require the optional public IL2CPP property exports.
- Property value reading additionally requires the optional runtime invocation, object inspection, thread attachment, unboxing, assignability, virtual dispatch, and GC-handle APIs.
- Runtime-API static-field storage requires both optional static-storage exports.
- Safe field-value reading requires the optional IL2CPP type-classification APIs; instance reads additionally require class instance-size inspection.
- Managed string decoding, vector-array inspection, and blittable value-type reads each depend on their own optional runtime exports and fail independently when unavailable.
- Type relationships require the corresponding optional class relationship exports and class identity APIs when a related type is materialized.
- Type and method metadata inspection require their optional metadata exports.
- Assembly/type/method/field resolution can continue to work when those optional capabilities are absent.

---

## Public Namespaces

Normal consumers should only need:

```csharp
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Invocation;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
```

The native process, memory, PE, remote execution, discovery, runtime catalogues, detectors, mapping implementations, and resolution session are internal implementation details.

---

# Quick Start

When the target layouts are already known, configure the resolver once and use the navigation API normally afterward:

```csharp
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

resolver.SetMethodInfoLayout(
    Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

resolver.SetFieldStorageLayout(
    Il2CppClassLayouts.Class29_2X64);

ResolvedAssembly inputSystemAssembly = resolver.ResolveAssembly(
    new AssemblyQuery("Unity.InputSystem"));

ResolvedType inputSystemType = inputSystemAssembly.ResolveType(
    "UnityEngine.InputSystem",
    "InputSystem");

ResolvedMethod queueEvent = inputSystemType.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

ResolvedMethodCode queueEventCode = queueEvent.ResolveCode();

IReadOnlyList<ResolvedProperty> properties = inputSystemType.GetProperties();
ResolvedProperty property = properties[0];
ResolvedMethod? getter = property.Getter;
ResolvedMethod? setter = property.Setter;

ResolvedField managerField = inputSystemType.ResolveField("s_Manager");
ResolvedFieldStorage managerStorage = managerField.ResolveStorage();
nint managerObject = managerField.ReadStaticReference();

ResolvedType? baseType = inputSystemType.GetBaseType();
ResolvedTypeMetadata typeMetadata = inputSystemType.GetMetadata();
ResolvedMethodMetadata methodMetadata = queueEvent.GetMetadata();

Console.WriteLine($"MethodInfo*:      0x{queueEvent.MethodInfoAddress:X}");
Console.WriteLine($"Native code:      0x{queueEventCode.NativeAddress:X}");
Console.WriteLine($"FieldInfo*:       0x{managerField.FieldInfoAddress:X}");
Console.WriteLine($"Static storage:   0x{managerStorage.StorageAddress:X}");
Console.WriteLine($"InputManager*:    0x{managerObject:X}");
Console.WriteLine($"Base type:        {baseType?.Query.Name}");
Console.WriteLine($"Type token:       0x{typeMetadata.MetadataToken:X8}");
Console.WriteLine($"Method token:     0x{methodMetadata.MetadataToken:X8}");
```

The important distinction is:

```text
semantic identity
    ≠
native representation
```

Resolving a method does not automatically assume a `MethodInfo` layout, and resolving a field does not manufacture an absolute storage address.

---

# Core Concepts

## Semantic Queries

Queries describe managed identities without containing target-specific pointers or offsets.

### `AssemblyQuery`

```csharp
AssemblyQuery query = new("Unity.InputSystem");
```

The resolver accepts a simple assembly identity and normalizes common `.dll` / `.exe` suffix differences during lookup.

### `TypeQuery`

```csharp
TypeQuery query = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem");
```

A type query identifies:

```text
assembly
namespace
managed type name
```

### `MethodQuery`

```csharp
MethodQuery query = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");
```

Method overload identity is currently based on:

```text
exact method name
+
exact ordered semantic parameter-type names
```

Return type is not used as overload identity.

### `FieldQuery`

```csharp
FieldQuery query = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");
```

A field query identifies a field by its exact declaring type and field name.

### `PropertyQuery`

```csharp
PropertyQuery query = new(
    "Some.Assembly",
    "Some.Namespace",
    "SomeType",
    "Item",
    "System.Int32");
```

Property identity is based on:

```text
exact property name
+
exact ordered index-parameter type names
```

For a non-indexed property, the index-parameter sequence is empty. Property type is derived from the getter return type and/or the setter value parameter; when both accessors exist, the resolver validates that their signatures are consistent.

---

## Resolved Runtime Entities

The main resolved entities are:

```text
ResolvedAssembly
ResolvedType
ResolvedMethod
ResolvedProperty
ResolvedField
ResolvedArray
```

They expose immutable runtime identity information and are bound to the resolver session that produced them.

They also provide navigation endpoints:

```text
ResolvedAssembly
    ├── GetTypes()
    └── ResolveType(...)

ResolvedType
    ├── GetMethods()
    ├── GetMethods(name)
    ├── ResolveMethod(...)
    ├── GetProperties()
    ├── GetProperties(name)
    ├── ResolveProperty(...)
    ├── GetFields()
    └── ResolveField(...)

ResolvedMethod
    └── ResolveCode(...)

ResolvedProperty
    ├── Getter → ResolvedMethod?
    └── Setter → ResolvedMethod?

ResolvedField
    ├── ResolveStorage(...)
    ├── Read* / ReadStatic*(...)
    └── ReadArray / ReadStaticArray(...) → ResolvedArray?

ResolvedArray
    ├── Read<T>(index)
    ├── ReadEnum<TEnum>(index)
    ├── ReadReference(index)
    ├── ReadString(index)
    └── ReadBlittable<T>(index)
```

The runtime logic remains centralized in the owning resolver session; resolved entities are navigation handles, not independent runtime backends.

---

## Native Mapping Results

Native mappings are deliberately separate from semantic entities.

### `ResolvedMethodCode`

Contains:

```text
ResolvedMethod
NativeAddress
SectionName
CompatibilityProfile
MethodPointerOffset
```

### `ResolvedFieldStorage`

Contains:

```text
ResolvedField
StaticFieldsAddress
StaticFieldsSize
StaticStorageOffset
StorageAddress
ResolutionSource
CompatibilityProfile (layout path only)
StaticFieldsPointerOffset (layout path only)
StaticFieldsSizeOffset (layout path only)
```

---

# Resolver Lifetime

Attach one resolver to one target process:

```csharp
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);
```

The default timeout for one individual remote IL2CPP runtime call is five seconds.

A custom timeout can be supplied:

```csharp
using Il2CppResolver resolver = Il2CppResolver.Attach(
    processId,
    TimeSpan.FromSeconds(3));
```

`Il2CppResolver` owns the target-specific session and process attachment. Disposing the resolver releases the session and makes further resolver or navigation operations invalid.

```csharp
resolver.Dispose();
```

Normally, prefer `using` so ownership is explicit.

---

# Layout Configuration

Layouts are immutable compatibility descriptions. They do not perform detection or memory access themselves.

Two structural families currently exist:

```text
Il2CppMethodInfoLayout
Il2CppClassLayout
```

## Built-in MethodInfo Layouts

### `DirectMethodPointerFirstX64`

```csharp
Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64
```

Structural definition:

```text
methodPointer offset = 0x0
```

This layout represents an IL2CPP `MethodInfo` structure whose direct native method pointer occupies the first pointer-sized field.

## Built-in Il2CppClass Layouts

### `Class29_1X64`

```csharp
Il2CppClassLayouts.Class29_1X64
```

```text
static_fields      = +0xB8
static_fields_size = +0x108
```

### `Class29_2X64`

```csharp
Il2CppClassLayouts.Class29_2X64
```

```text
static_fields      = +0xB8
static_fields_size = +0x10C
```

Built-in names describe known structural profiles. Layouts remain explicit compatibility assumptions and are still validated when they are used to produce native mappings.

---

## Global Layout Selection

Select a MethodInfo layout once for the resolver session:

```csharp
resolver.SetMethodInfoLayout(
    Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
```

Select an Il2CppClass field-storage layout once:

```csharp
resolver.SetFieldStorageLayout(
    Il2CppClassLayouts.Class29_2X64);
```

The active explicit selections are observable:

```csharp
Il2CppMethodInfoLayout? methodLayout = resolver.MethodInfoLayout;
Il2CppClassLayout? fieldLayout = resolver.FieldStorageLayout;
```

Explicit selections survive `ClearCache()`.

---

## Resetting Global Layout Selection

```csharp
resolver.ResetMethodInfoLayout();
resolver.ResetFieldStorageLayout();
```

Resetting removes the explicit selection and restores automatic behavior.

For method code, automatic behavior means lazy layout detection.

For field storage, automatic behavior means:

1. use the public IL2CPP static-field storage API when available;
2. otherwise reuse a class layout that was previously established through explicit automatic detection evidence;
3. otherwise fail with a configuration diagnostic.

---

## One-shot Layout Overrides

A one-shot override does not modify the global resolver configuration.

### Method code

```csharp
ResolvedMethodCode code = method.ResolveCode(customMethodLayout);
```

or:

```csharp
ResolvedMethodCode code = resolver.ResolveMethodCode(
    methodQuery,
    customMethodLayout);
```

### Field storage

```csharp
ResolvedFieldStorage storage = field.ResolveStorage(customClassLayout);
```

or:

```csharp
ResolvedFieldStorage storage = resolver.ResolveFieldStorage(
    fieldQuery,
    customClassLayout);
```

Supplying an explicit class layout forces the structural layout path even when the target also exposes the public static-field storage API.

---

## Custom Layouts

Layouts are immutable and can be created by consumers.

### Custom MethodInfo layout

```csharp
Il2CppMethodInfoLayout layout = new(
    "CustomMethodInfo",
    directMethodPointerOffset: 0x0);
```

### Custom Il2CppClass layout

```csharp
Il2CppClassLayout layout = new(
    "CustomClassLayout",
    staticFieldsPointerOffset: 0xB8,
    staticFieldsSizeOffset: 0x10C);
```

To use a custom layout immediately:

```csharp
resolver.SetMethodInfoLayout(customMethodLayout);
resolver.SetFieldStorageLayout(customClassLayout);
```

---

## Registering Automatic-detection Candidates

Registration adds a layout to the session's candidate registry without selecting it globally.

```csharp
resolver.RegisterMethodInfoLayout(customMethodLayout);
resolver.RegisterFieldStorageLayout(customClassLayout);
```

The registry rejects conflicting candidates:

- the same name with different structural offsets;
- the same structural identity under a different name.

Re-registering the exact same definition is an idempotent no-op and returns `false`.

```csharp
bool added = resolver.RegisterMethodInfoLayout(layout);
```

---

# Automatic Layout Detection

## MethodInfo Detection

When no MethodInfo layout is explicitly selected, default method-code resolution lazily detects one from methods declared by the target method's declaring type.

```csharp
ResolvedMethodCode code = resolver.ResolveMethodCode(methodQuery);
```

or:

```csharp
ResolvedMethodCode code = method.ResolveCode();
```

The detector:

- uses registered MethodInfo layout candidates;
- samples multiple `MethodInfo*` addresses;
- treats null method pointers as absence of positive evidence;
- requires non-null candidates to point inside `GameAssembly.dll`;
- requires the containing PE section to be executable;
- rejects unreadable candidate slots locally rather than aborting the entire detection pass;
- requires a unique surviving layout.

The current automatic path requires at least five available methods on the evidence type. If the type does not provide enough evidence, configure a known layout explicitly with `SetMethodInfoLayout`.

---

## Il2CppClass Detection for Field Storage

Normal default field-storage resolution does not automatically invent evidence queries.

If the target does not expose the public static-field storage API and no explicit layout is configured, a consumer can request class-layout detection using normal static fields from multiple declaring classes:

```csharp
FieldQuery target = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");

FieldQuery[] evidence =
{
    new FieldQuery("Some.Assembly", "Some.Namespace", "SomeType", "someStaticField"),
    new FieldQuery("Another.Assembly", "Another.Namespace", "AnotherType", "anotherStaticField")
};

ResolvedFieldStorage storage = resolver.ResolveFieldStorage(
    target,
    evidence);
```

The target field also contributes evidence when it is a normal static field.

The detector requires evidence from at least three distinct declaring classes and validates, for each candidate layout:

```text
static_fields slot is readable
static_fields != null
static_fields_size is non-zero and reasonable
static_fields block is readable
known static offsets belong to the block
calculated storage addresses are readable
```

Exactly one layout must survive.

The detected layout is retained for subsequent default field-storage calls until cache invalidation or registration of new class-layout candidates.

---

# Assemblies

## Enumerating Assemblies

```csharp
IReadOnlyList<ResolvedAssembly> assemblies = resolver.GetAssemblies();
```

The active domain and complete assembly snapshot are materialized once per cache generation. Repeated calls reuse local session data.

A `ResolvedAssembly` exposes:

```csharp
assembly.Query
assembly.Name
assembly.AssemblyAddress
assembly.ImageAddress
```

## Targeted Assembly Resolution

```csharp
ResolvedAssembly assembly = resolver.ResolveAssembly(
    new AssemblyQuery("Unity.InputSystem"));
```

Use targeted resolution when the assembly identity is already known.

---

# Types

## Targeted Type Resolution

From the resolver:

```csharp
ResolvedType type = resolver.ResolveType(
    new TypeQuery(
        "Unity.InputSystem",
        "UnityEngine.InputSystem",
        "InputSystem"));
```

From an already resolved assembly:

```csharp
ResolvedType type = assembly.ResolveType(
    "UnityEngine.InputSystem",
    "InputSystem");
```

A `ResolvedType` exposes:

```csharp
type.Query
type.Assembly
type.ClassAddress
```

Targeted type resolution uses `il2cpp_class_from_name` and does not require enumerating every type in the image.

---

## Enumerating Types

```csharp
IReadOnlyList<ResolvedType> types = assembly.GetTypes();
```

Type enumeration requires the optional IL2CPP image/class enumeration capability:

```text
il2cpp_image_get_class_count
il2cpp_image_get_class
il2cpp_class_get_name
il2cpp_class_get_namespace
```

If those exports are unavailable, targeted `ResolveType` can still work while `GetTypes()` throws `NotSupportedException`.

The image type catalogue is cached for the current resolver generation.

---

## Type Relationships

Resolved types can navigate runtime class relationships without exposing raw IL2CPP traversal primitives:

```csharp
ResolvedType? baseType = type.GetBaseType();
IReadOnlyList<ResolvedType> interfaces = type.GetInterfaces();
IReadOnlyList<ResolvedType> nestedTypes = type.GetNestedTypes();
ResolvedType? declaringType = type.GetDeclaringType();
```

Related types are materialized through the same session identity map used by targeted resolution. If a parent or interface was already resolved elsewhere in the current cache generation, relationship navigation returns the same public `ResolvedType` instance.

Relationship snapshots are lazy and cached per native `Il2CppClass*`. The first call may perform remote IL2CPP traversal; subsequent calls in the same generation reuse the local snapshot.

`GetDeclaringType()` returns `null` for top-level types. `GetBaseType()` returns `null` for runtime root types.

## Type Metadata

Type metadata is explicitly requested and cached:

```csharp
ResolvedTypeMetadata metadata = type.GetMetadata();

Console.WriteLine(metadata.Attributes);
Console.WriteLine(metadata.MetadataToken);
Console.WriteLine(metadata.IsValueType);
Console.WriteLine(metadata.IsEnum);
Console.WriteLine(metadata.IsBlittable);
Console.WriteLine(metadata.IsGeneric);
Console.WriteLine(metadata.IsInflated);
```

For value types, IL2CPP value size and alignment are also exposed when the target provides the corresponding runtime API:

```csharp
if (metadata.IsValueType)
{
    Console.WriteLine(metadata.ValueSize);
    Console.WriteLine(metadata.ValueAlignment);
}
```

`ResolvedTypeMetadata` is a pure immutable snapshot. Reading its properties never performs additional remote work.

# Methods

## Exact Method Resolution

```csharp
ResolvedMethod method = type.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");
```

Equivalent resolver-level form:

```csharp
ResolvedMethod method = resolver.ResolveMethod(
    new MethodQuery(
        "Unity.InputSystem",
        "UnityEngine.InputSystem",
        "InputSystem",
        "QueueEvent",
        "UnityEngine.InputSystem.LowLevel.InputEventPtr"));
```

The resolver first narrows candidates by exact method name, then inspects complete semantic signatures only for those candidates.

This matters for overloaded APIs. For example, a type may expose both:

```text
QueueEvent(InputEventPtr)
QueueEvent(TEvent&)
```

A simple method-name lookup is not sufficient to identify the requested overload.

A `ResolvedMethod` exposes:

```csharp
method.Query
method.DeclaringType
method.MethodInfoAddress
method.ReturnTypeName
method.ParameterTypeNames
```

---

## Enumerating Methods by Name

```csharp
IReadOnlyList<ResolvedMethod> overloads = type.GetMethods("QueueEvent");
```

This is the preferred exploration endpoint when only one method name is relevant.

The member catalogue indexes raw methods by name, then materializes complete signatures only for matching candidates.

---

## Enumerating All Methods

```csharp
IReadOnlyList<ResolvedMethod> methods = type.GetMethods();
```

This intentionally materializes complete descriptions for every method declared by the type.

Use it for exploration, not as a replacement for targeted `ResolveMethod` when the desired signature is already known.

---

## Method Metadata

Method metadata is also lazy and cached by native `MethodInfo*` identity:

```csharp
ResolvedMethodMetadata metadata = method.GetMetadata();

Console.WriteLine(metadata.Attributes);
Console.WriteLine(metadata.ImplementationAttributes);
Console.WriteLine(metadata.MetadataToken);
Console.WriteLine(metadata.IsStatic);
Console.WriteLine(metadata.IsVirtual);
Console.WriteLine(metadata.IsAbstract);
Console.WriteLine(metadata.IsGeneric);
Console.WriteLine(metadata.IsInflated);
```

The convenience properties are derived from the method flags reported by IL2CPP. Metadata inspection is independent from native code mapping: calling `GetMetadata()` does not require or select a `MethodInfo` structural layout.

# Native Method Code

A resolved method can be mapped to validated direct native code:

```csharp
ResolvedMethodCode code = method.ResolveCode();
```

With a one-shot layout override:

```csharp
ResolvedMethodCode code = method.ResolveCode(
    Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
```

A `ResolvedMethodCode` exposes:

```csharp
code.Method
code.NativeAddress
code.SectionName
code.CompatibilityProfile
code.MethodPointerOffset
```

The structural resolver validates that the candidate pointer:

- is non-zero;
- belongs to the active `GameAssembly.dll` image;
- belongs to a parsed PE section;
- belongs to an executable section.

## Important: native code is not an invocation contract

A validated native method address does **not** mean the method can safely be called with an arbitrary C#-like signature.

Invocation still depends on details such as:

- instance vs. static semantics;
- Windows x64 ABI requirements;
- possible hidden runtime arguments;
- value-type conventions;
- generic sharing;
- runtime-specific calling patterns.

`ResolvedMethodCode` answers **where the direct native code is**, not **how every managed method should be invoked externally**.

---

# Properties

Property support is capability-based and does not make resolver attachment stricter. When the target exposes the public IL2CPP property APIs, properties participate in the same navigation and identity-map model as methods and fields.

## Exact Property Resolution

For a non-indexed property:

```csharp
ResolvedProperty property = type.ResolveProperty("settings");
```

For an indexed property:

```csharp
ResolvedProperty property = type.ResolveProperty(
    "Item",
    "System.Int32");
```

Equivalent resolver-level form:

```csharp
ResolvedProperty property = resolver.ResolveProperty(
    new PropertyQuery(
        "Some.Assembly",
        "Some.Namespace",
        "SomeType",
        "Item",
        "System.Int32"));
```

Resolution first narrows candidates by exact property name and then compares the exact ordered index-parameter type sequence.

A `ResolvedProperty` exposes:

```csharp
property.Query
property.DeclaringType
property.PropertyInfoAddress
property.TypeName
property.IndexParameterTypeNames
property.Attributes
property.Getter
property.Setter
property.CanRead
property.CanWrite
```

## Enumerating Properties by Name

```csharp
IReadOnlyList<ResolvedProperty> properties =
    type.GetProperties("Item");
```

Only properties with the requested name have their accessor signatures materialized.

## Enumerating All Properties

```csharp
IReadOnlyList<ResolvedProperty> properties = type.GetProperties();
```

This explicitly materializes every property signature declared by the class. Property enumeration is cached in the class-member catalogue for the current resolver generation.

## Property Accessors

Getter and setter methods reuse the existing method identity map:

```csharp
ResolvedMethod? getter = property.Getter;
ResolvedMethod? setter = property.Setter;
```

If the same accessor is later resolved through `ResolveMethod`, the resolver returns the same `ResolvedMethod` object within the active cache generation.

A property does **not** imply storage and does not expose a `ResolveStorage` endpoint. Property value reads invoke the getter and therefore execute managed target code; they are fundamentally different from field-memory reads.

## Property Value Reading

`ResolvedProperty` can invoke a **parameterless getter** through `il2cpp_runtime_invoke`. Static and instance calls are explicit so accidental instance/static mismatches are rejected before managed execution:

```csharp
float pollingFrequency = inputSystem.ResolveProperty("pollingFrequency").ReadStatic<float>();

nint settingsObject = inputSystem.ResolveProperty("settings").ReadStaticReference();

ResolvedType settingsType = resolver.ResolveType(
    new TypeQuery(
        "Unity.InputSystem",
        "UnityEngine.InputSystem",
        "InputSettings"));

float defaultPressPoint = settingsType
    .ResolveProperty("defaultButtonPressPoint")
    .Read<float>(settingsObject);
```

Supported return categories mirror the validated field-value readers:

```csharp
property.Read<T>(instance);
property.ReadStatic<T>();
property.ReadEnum<TEnum>(instance);
property.ReadStaticEnum<TEnum>();
property.ReadReference(instance);
property.ReadStaticReference();
property.ReadString(instance);
property.ReadStaticString();
property.ReadArray(instance);
property.ReadStaticArray();
property.ReadBlittable<T>(instance);
property.ReadStaticBlittable<T>();
```

Getter invocation deliberately supports only non-indexed/parameterless accessors in this version. Indexers, setters, arbitrary arguments, and general `ResolvedMethod.Invoke(...)` remain outside the public API.

Instance getter invocation first validates that the supplied object header is readable, then creates a temporary strong GC handle and resolves the protected object back through `il2cpp_gchandle_get_target`. Runtime class validation, assignability checks, virtual dispatch through `il2cpp_object_get_virtual_method`, and the managed call all use that rooted object. The temporary instance root is released after completed object-sensitive runtime operations and never enters the session retained-result table. If completion of object-class inspection, virtual dispatch, or managed invocation cannot be proven, the root is intentionally abandoned because the target thread may still depend on the instance. Value-type declaring instances are intentionally unsupported.

The remote invocation thread is attached to the active IL2CPP domain only for the managed call and detached afterward. A managed exception reported through the `Il2CppException**` output becomes `Il2CppInvocationException`, which exposes the remote exception object identity without attempting to marshal it.

Non-null getter results are rooted with a strong IL2CPP GC handle before the invocation thread detaches. The resolver treats `Il2CppGCHandle` as an opaque pointer-sized value and preserves the complete native return register; this remains compatible with older runtimes whose GC-handle API returned a 32-bit identifier while supporting newer Unity runtimes where the public API uses the opaque `Il2CppGCHandle` type. Scalar, enum, string, and blittable results release that root after local decoding through the native `il2cpp_gchandle_free` API; GC-handle cleanup does not create a second attached IL2CPP thread. Raw reference and `ResolvedArray` results retain their roots until `ClearCache()` or resolver disposal so the target object cannot be collected immediately after the getter returns. If result decoding already failed, a later cleanup failure is suppressed so it cannot replace the primary error. If any GC-handle cleanup cannot be completed safely, the handle is intentionally abandoned rather than retried blindly.

> **Important:** a property read executes managed target code. A getter may allocate, mutate state, take locks, raise an exception, or perform arbitrary application logic. This API is therefore more invasive than reading a field from validated memory.

---

# Fields

## Targeted Field Resolution

```csharp
ResolvedField field = type.ResolveField("s_Manager");
```

Equivalent resolver-level form:

```csharp
ResolvedField field = resolver.ResolveField(
    new FieldQuery(
        "Unity.InputSystem",
        "UnityEngine.InputSystem",
        "InputSystem",
        "s_Manager"));
```

A `ResolvedField` exposes:

```csharp
field.Query
field.DeclaringType
field.FieldInfoAddress
field.TypeName
field.Attributes
field.StorageKind
field.InstanceOffset
field.StaticStorageOffset
```

---

## Enumerating Fields

```csharp
IReadOnlyList<ResolvedField> fields = type.GetFields();
```

The class member catalogue is session-scoped and reused by subsequent operations during the same generation.

---

## Field Storage Kinds

`FieldStorageKind` distinguishes storage semantics explicitly:

### `Instance`

The field is stored in each object or value-type instance.

```csharp
nuint? offset = field.InstanceOffset;
```

### `Static`

The field is stored in the declaring class's normal IL2CPP static-data block.

```csharp
nuint? offset = field.StaticStorageOffset;
```

### `ThreadStatic`

The field uses thread-local runtime storage. It does not have one process-global address.

### `Literal`

The field represents metadata literal data rather than ordinary mutable runtime storage.

The current concrete field-storage resolver intentionally supports only normal `Static` fields.

---

## Field Value Reading

`ResolvedField` can read a deliberately conservative set of values directly from validated runtime storage. Value reading is separate from semantic field resolution: resolving a field does not inspect or read its current value until one of the explicit `Read*` endpoints is called.

### Static managed references

```csharp
ResolvedField managerField = inputSystemType.ResolveField("s_Manager");
nint managerObject = managerField.ReadStaticReference();
```

A managed reference read returns the remote `Il2CppObject*` address. It does not materialize a local managed object.

### Static scalar values

```csharp
int count = someStaticIntField.ReadStatic<int>();
bool enabled = someStaticBoolField.ReadStatic<bool>();
```

A one-shot class-layout override is available when static storage must be interpreted through a specific structural profile:

```csharp
int count = someStaticIntField.ReadStatic<int>(Il2CppClassLayouts.Class29_2X64);
```

### Instance scalar and reference values

When the caller already owns a remote `Il2CppObject*` address:

```csharp
int value = instanceIntField.Read<int>(objectAddress);
nint reference = instanceReferenceField.ReadReference(objectAddress);
```

Instance reads are intentionally limited to fields declared by reference types. Reading fields relative to unboxed value-type storage is not part of the current contract.

### Enum values

Enum reads use dedicated APIs so an arbitrary unmanaged type cannot be accepted only because it has a compatible size:

```csharp
MyMatchingEnum value = enumField.ReadEnum<MyMatchingEnum>(objectAddress);
MyMatchingEnum staticValue = staticEnumField.ReadStaticEnum<MyMatchingEnum>();
```

The managed enum name and underlying scalar type must match the runtime IL2CPP enum.

### Managed strings

String reads preserve the distinction between a null reference and an empty managed string:

```csharp
string? staticText = staticStringField.ReadStaticString();
string? instanceText = instanceStringField.ReadString(objectAddress);
```

The reader uses the public IL2CPP string APIs to retrieve the current UTF-16 length and character buffer, applies a defensive character-count limit, validates the complete remote character range, and only then allocates and decodes the local .NET string.

```text
null Il2CppString* → null
length == 0       → string.Empty
otherwise         → validated UTF-16 decode
```

### Managed arrays (`ResolvedArray`)

Normal static and instance fields whose runtime type is an IL2CPP single-dimensional zero-based array (`SZARRAY`) can be inspected without eagerly materializing a local `T[]`:

```csharp
ResolvedArray? staticArray = staticArrayField.ReadStaticArray();
ResolvedArray? instanceArray = instanceArrayField.ReadArray(objectAddress);
```

A non-null `ResolvedArray` exposes immutable shape/type information and generation-aware element reads:

```csharp
Console.WriteLine(array.Address);
Console.WriteLine(array.Length);
Console.WriteLine(array.ElementTypeName);

int number = array.Read<int>(index);
MyMatchingEnum state = array.ReadEnum<MyMatchingEnum>(index);
nint reference = array.ReadReference(index);
string? text = array.ReadString(index);
MyMatchingBlittable value = array.ReadBlittable<MyMatchingBlittable>(index);
```

Every element read validates the index, exact runtime element type, runtime-reported element-slot size, calculated address, and complete memory range. Multidimensional arrays are deliberately rejected. Jagged arrays remain ordinary vectors whose elements are managed array references.

The current Windows x64 implementation uses the stable `Il2CppArray` vector payload offset `0x20` only after cross-validating the live array's public IL2CPP-reported element count, payload byte length, array element class/type, and element size. This is an explicit structural assumption rather than a new public layout contract.

### Blittable value types

Arbitrary `unmanaged` types are **not** accepted by `Read<T>()`. Blittable structs use an explicit API:

```csharp
MyMatchingBlittable staticValue = valueField.ReadStaticBlittable<MyMatchingBlittable>();
MyMatchingBlittable instanceValue = valueField.ReadBlittable<MyMatchingBlittable>(objectAddress);
```

A blittable read is accepted only when all of the following are true:

```text
IL2CPP type is a non-enum value type
+ IL2CPP reports the class as blittable
+ runtime value size is positive
+ sizeof(T) exactly matches the IL2CPP value size
+ managed T semantic full name matches the IL2CPP type name
```

The resolver does not perform managed marshalling or field-by-field structural conversion. It copies the already validated native bytes into the matching unmanaged local representation. Primitive scalars and enums must continue to use their dedicated APIs.

### Supported scalar types

The conservative scalar surface currently supports:

```text
bool, char
sbyte, byte
short, ushort
int, uint
long, ulong
float, double
nint, nuint
```

For all direct field reads, the reader validates:

```text
field storage kind
+ runtime Il2CppType category
+ exact requested representation
+ static/instance storage bounds
+ complete remote memory readability
```

For static values, the complete range must satisfy:

```text
StaticStorageOffset + sizeof(value) <= StaticFieldsSize
```

For instance values, the field range is validated against the IL2CPP-reported class instance size before memory is read.

`ThreadStatic` and `Literal` fields are intentionally rejected by the value reader.

# Static Field Storage

Resolve the concrete storage of a normal static field:

```csharp
ResolvedFieldStorage storage = field.ResolveStorage();
```

or:

```csharp
ResolvedFieldStorage storage = resolver.ResolveFieldStorage(field.Query);
```

The fundamental relationship is:

```text
StorageAddress = StaticFieldsAddress + StaticStorageOffset
```

A `ResolvedFieldStorage` exposes:

```csharp
storage.Field
storage.StaticFieldsAddress
storage.StaticFieldsSize
storage.StaticStorageOffset
storage.StorageAddress
storage.ResolutionSource
storage.CompatibilityProfile
storage.StaticFieldsPointerOffset
storage.StaticFieldsSizeOffset
```

---

## Default Static-storage Selection Order

Default `ResolveStorage()` / `ResolveFieldStorage(query)` follows this exact order:

1. If a global `Il2CppClassLayout` was selected with `SetFieldStorageLayout`, use that layout.
2. Otherwise, if the target exposes the public IL2CPP static-field storage API, use the runtime API.
3. Otherwise, if a class layout was previously established through automatic detection, use that detected layout.
4. Otherwise, fail with `InvalidOperationException` and require configuration or explicit detection evidence.

The optional runtime API requires both:

```text
il2cpp_class_get_static_field_data
il2cpp_class_get_data_size
```

---

## Resolution Source

`ResolvedFieldStorage.ResolutionSource` tells the consumer how storage was obtained.

### Runtime API

```csharp
storage.ResolutionSource == FieldStorageResolutionSource.RuntimeApi
```

In this case:

```csharp
storage.CompatibilityProfile == null
storage.StaticFieldsPointerOffset == null
storage.StaticFieldsSizeOffset == null
```

### Structural class layout

```csharp
storage.ResolutionSource == FieldStorageResolutionSource.ClassLayout
```

In this case the result also exposes the exact structural evidence used:

```csharp
storage.CompatibilityProfile
storage.StaticFieldsPointerOffset
storage.StaticFieldsSizeOffset
```

---

# Navigation API

The public API is intentionally linear:

```text
Il2CppResolver
      ↓
ResolvedAssembly
      ↓
ResolvedType
      ├───────────────┐
      ↓               ↓
ResolvedMethod    ResolvedField
      ↓               ↓
ResolvedMethodCode  ResolvedFieldStorage
```

A typical navigation flow is:

```csharp
ResolvedAssembly assembly = resolver.ResolveAssembly(
    new AssemblyQuery("Unity.InputSystem"));

ResolvedType type = assembly.ResolveType(
    "UnityEngine.InputSystem",
    "InputSystem");

ResolvedMethod method = type.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

ResolvedMethodCode code = method.ResolveCode();

ResolvedField field = type.ResolveField("s_Manager");
ResolvedFieldStorage storage = field.ResolveStorage();
```

Navigation does not duplicate runtime state. Every resolved entity delegates through the same session and reuses the same catalogues and caches.

---

# Caching and Runtime Catalogues

The resolver maintains session-scoped local state to reduce expensive remote runtime calls.

The current cache/catalogue layers include:

- active IL2CPP domain snapshot;
- loaded assembly snapshot;
- assembly lookup indexes;
- image type catalogues;
- raw method addresses per `Il2CppClass*`;
- raw field addresses per `Il2CppClass*`;
- member-name indexes;
- lazily inspected method descriptions;
- lazily inspected field descriptions;
- IL2CPP type-name cache;
- runtime type descriptors and class metadata;
- array element type and element-size descriptors;
- semantic query results;
- runtime identity maps;
- layout-sensitive native method mappings;
- layout-sensitive static-field storage mappings;
- runtime-API static-field storage mappings.

The design intentionally optimizes local reuse before introducing any persistent remote execution state.

---

# Targeted Resolution vs. Enumeration

Use targeted resolution when the identity is already known:

```csharp
resolver.ResolveAssembly(...)
resolver.ResolveType(...)
type.ResolveMethod(...)
type.ResolveField(...)
```

Use enumeration for exploration:

```csharp
resolver.GetAssemblies()
assembly.GetTypes()
type.GetMethods()
type.GetMethods("Name")
type.GetFields()
```

The intended cost model is:

```text
Targeted ResolveX
    → narrow the search as early as possible

GetXs
    → explicitly materialize an exploratory snapshot
```

For example, `ResolveMethod` does not call `GetMethods()` and inspect every signature. It uses the class member name index first and inspects complete signatures only for matching-name candidates.

---

# Runtime Identity Map

Within one cache generation, runtime identities are reused by native address.

The session maintains identity maps for concepts such as:

```text
Il2CppImage*  → ResolvedAssembly
Il2CppClass*  → ResolvedType
MethodInfo*   → ResolvedMethod
FieldInfo*    → ResolvedField
```

Therefore, targeted resolution and navigation can converge on the same public object instance:

```csharp
ResolvedType first = resolver.ResolveType(typeQuery);
ResolvedType second = assembly.ResolveType(
    "UnityEngine.InputSystem",
    "InputSystem");

bool sameObject = ReferenceEquals(first, second);
```

Within the same generation, `sameObject` is expected to be `true` when both paths resolve the same runtime entity.

---

# ClearCache and Cache Generations

```csharp
resolver.ClearCache();
```

`ClearCache()` invalidates:

- semantic resolution results;
- runtime snapshots and catalogues;
- identity maps;
- native mapping caches;
- automatically detected MethodInfo layouts;
- automatically detected Il2CppClass layouts.

It preserves:

- explicitly selected MethodInfo layout;
- explicitly selected field-storage layout;
- registered MethodInfo layout candidates;
- registered class-layout candidates.

The session then advances its navigation generation.

---

## Generation Invalidation

Resolved entities remain useful as immutable snapshots after cache invalidation, but they may no longer navigate using potentially stale runtime pointers.

```csharp
ResolvedType oldType = resolver.ResolveType(typeQuery);

resolver.ClearCache();

nint oldAddress = oldType.ClassAddress; // snapshot data remains readable

oldType.GetMethods(); // InvalidOperationException
```

To continue navigating, resolve the entity again:

```csharp
ResolvedType refreshed = resolver.ResolveType(typeQuery);
```

The refreshed result belongs to the new cache generation.

This prevents a previously resolved `Il2CppClass*`, `MethodInfo*`, `PropertyInfo*`, `FieldInfo*`, image identity, or session-bound `ResolvedArray` from being silently reused after the caller explicitly requested runtime cache invalidation.

---

# Concurrency Model

A resolver session serializes its operations through one session lock.

This means:

- cache and layout state are updated coherently;
- `ClearCache()` cannot race through a concurrent session operation;
- automatic layout detection is not performed concurrently within one resolver;
- remote calls for one resolver instance are effectively serialized.

This favors correctness and deterministic session state over parallel remote execution.

---

# Error Handling

The resolver uses standard exception types to communicate different failure classes.

| Exception | Typical meaning |
|---|---|
| `ArgumentException` / `ArgumentOutOfRangeException` | Invalid query, layout, offset, timeout, or configuration argument. |
| `KeyNotFoundException` | A requested semantic assembly, type, method, property, or field was not found. |
| `InvalidDataException` | Runtime evidence, a layout interpretation, PE state, or automatic detection result was inconsistent. |
| `NotSupportedException` | The target lacks an optional capability or the requested storage category is intentionally unsupported. |
| `InvalidOperationException` | The requested operation is incompatible with current resolver state, no field-storage strategy is available, a target has exited, or a resolved entity belongs to an invalidated generation. |
| `ObjectDisposedException` | The resolver/session or an owned native component has already been disposed. |
| `TimeoutException` | A remote runtime call did not complete within its finite timeout. |
| `Win32Exception` | A Windows process, memory, thread, module, or synchronization operation failed. |

Consumers should not normally catch every exception and continue blindly. Failures such as target termination, invalid structural evidence, or unresolved remote-thread termination can indicate that the current resolver session should no longer be trusted for further operations.

---

# Performance Characteristics

The resolver is optimized around session-local reuse rather than persistent remote execution.

Typical first-use costs include:

- building the assembly snapshot;
- enumerating all types from an image when `GetTypes()` is explicitly requested;
- building a method, property, or field name index for a class;
- materializing complete signatures for enumerated methods and properties;
- converting IL2CPP type handles to semantic names.

Repeated operations reuse local snapshots and identity maps whenever possible.

In practical terms:

```text
Resolve known entity
    → preferred for direct use

Enumerate everything
    → preferred for exploration/tooling
```

`GetTypes()` is usually substantially more expensive than `ResolveType()` because it intentionally enumerates and names every class exposed by the image.

---

# Architecture

The public API intentionally hides native machinery behind one resolver session.

```text
                     Il2CppResolver
                           │
                    ResolutionSession
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
 ResolutionCache     RuntimeCatalog     LayoutRegistry
        │                  │                  │
        └──────────────┬───┴───────┬──────────┘
                       │           │
             RuntimeResolutionBackend
                       │
                  Il2CppRuntime
                       │
                    RemoteCall
```

Native mapping remains separate from semantic resolution:

```text
ResolvedMethod
      ↓
Il2CppMethodPointerResolver
      ↓
ResolvedMethodCode
```

Properties reuse method resolution for their accessors:

```text
ResolvedProperty
    ├── Getter → ResolvedMethod
    └── Setter → ResolvedMethod
```

Static-field storage uses two possible backends:

```text
ResolvedField
      ↓
selected explicit class layout?
      ├── yes → Il2CppStaticFieldStorageResolver
      │
      └── no
           ↓
public static-field runtime API available?
      ├── yes → Il2CppRuntimeStaticFieldStorageResolver
      │
      └── no → previously detected class layout fallback
```

Field values are interpreted only after storage and runtime type validation:

```text
ResolvedField
      ↓
concrete static storage / instance base + offset
      ↓
Il2CppTypeCatalog
      ↓
FieldValueTypeValidator
      ↓
      ├── scalar / enum / reference → ProcessMemory.Read<T>()
      ├── System.String            → Il2CppStringReader
      ├── SZARRAY                  → Il2CppArrayReader → ResolvedArray
      └── blittable value type     → exact metadata/size/name validation
```

Type relationships and metadata are lazy catalogue operations:

```text
ResolvedType ──→ Il2CppTypeCatalog ──→ parent / interfaces / nested / declaring / metadata
ResolvedMethod ─→ Il2CppMethodMetadataCatalog ─→ method metadata
```

---

# Project Structure

```text
Native/
├── Process/                 process ownership and architecture validation
├── Memory/                  read-only process memory and memory-region validation
├── Modules/                 loaded module discovery
├── PE/                      loaded PE parsing, sections and exports
└── Remote/                  short-lived Windows x64 remote calls

Il2Cpp/
├── Il2CppResolver.cs        public facade
├── Queries/                 public semantic identities
├── Results/                 public resolved entities and native mapping results
├── Navigation/              internal session binding for resolved-entity endpoints
├── Layouts/                 public layout definitions/catalogues + internal registry
├── Discovery/               internal IL2CPP target discovery
├── Runtime/
│   ├── Model/               internal raw runtime descriptions
│   └── Catalog/             session-scoped runtime snapshots and indexes
├── Detection/               internal layout detectors
├── Mapping/                 internal method-code and field-storage mappers
├── Invocation/              controlled parameterless property-getter invocation
├── Values/                  internal field/type validation and safe value decoding
└── Resolution/              semantic backend, cache and session orchestration
```

---

# Runtime Execution Model

The current runtime backend invokes exported IL2CPP APIs inside the target process using narrowly scoped Windows x64 trampolines.

The design deliberately avoids a persistent remote worker.

A typical remote call uses:

```text
temporary remote data/code allocation
        ↓
write trampoline / arguments
        ↓
protect code as executable
        ↓
CreateRemoteThread
        ↓
finite wait
        ↓
read result
        ↓
cleanup only after termination is proven
```

The primary target process handle remains read-oriented. Stronger process rights are obtained through short-lived additional handles only by components that require them.

---

# Safety Model

The project follows a conservative escalation principle:

```text
read
>
temporary allocation
>
temporary execution
>
persistent mutation
```

The resolver primarily uses reads and short-lived remote execution required to call IL2CPP runtime APIs. Property value reads additionally execute the resolved managed getter through `il2cpp_runtime_invoke`; callers should treat that endpoint as application code execution rather than passive inspection.

## Remote timeout safety

A critical invariant is:

> Remote memory must not be freed while a remote thread may still reference it.

When thread termination cannot be proven because of a timeout, failed wait, or unexpected synchronization state, remote allocations that may still be referenced by that thread are intentionally abandoned rather than freed.

This can leak target memory until the target process terminates, but avoids a remote use-after-free condition.

For this layer:

```text
controlled leak
    >
unsafe remote free
```

---

# Required and Optional Runtime Capabilities

The semantic runtime backend currently requires exported APIs for:

- domain access;
- assembly enumeration;
- assembly/image mapping;
- image naming;
- targeted class lookup;
- method enumeration and signature inspection;
- field enumeration and metadata inspection;
- IL2CPP type-name conversion;
- freeing runtime-owned type-name strings.

Additional capabilities are optional.

## Optional type enumeration

`ResolvedAssembly.GetTypes()` requires:

```text
il2cpp_image_get_class_count
il2cpp_image_get_class
il2cpp_class_get_name
il2cpp_class_get_namespace
```

## Optional property navigation

Property navigation requires the following optional public IL2CPP exports:

```text
il2cpp_class_get_properties
il2cpp_property_get_name
il2cpp_property_get_flags
il2cpp_property_get_get_method
il2cpp_property_get_set_method
```

If this capability is unavailable, assembly/type/method/field resolution remains usable while `GetProperties(...)` and `ResolveProperty(...)` throw `NotSupportedException`.

## Optional property getter invocation

Property value reading requires the following base invocation exports:

```text
il2cpp_runtime_invoke
il2cpp_object_unbox
il2cpp_thread_attach
il2cpp_thread_detach
il2cpp_method_is_instance
il2cpp_object_get_class
il2cpp_object_get_virtual_method
il2cpp_class_is_assignable_from
il2cpp_gchandle_new
il2cpp_gchandle_free
```

Instance getters additionally require:

```text
il2cpp_gchandle_get_target
```

These exports remain optional. Missing invocation support does not affect property navigation or any semantic resolver operation; only the affected property value reads throw `NotSupportedException`. Returned managed objects are strongly rooted before the temporary invocation thread detaches. Instance getters use `il2cpp_gchandle_get_target` so the caller-supplied object can be rooted before class validation and virtual dispatch; static getter invocation remains independent of that instance-only operation.

---

## Optional runtime static-field storage

Layout-independent runtime static storage requires:

```text
il2cpp_class_get_static_field_data
il2cpp_class_get_data_size
```

When these two exports are unavailable, explicit or previously detected class-layout resolution remains available.

---

## Optional field-value inspection

Field value reading is capability-based. Scalar and managed-reference interpretation requires runtime type classification APIs such as the IL2CPP type code, class mapping, and value-type/enum classification. Enum reads additionally require enum underlying-type inspection.

Instance field reads also require the runtime class instance-size API so the complete field range can be validated before `ReadProcessMemory` is called.

If the required exports are unavailable, the affected read endpoint throws `NotSupportedException`; normal semantic field resolution remains available.

Managed string decoding additionally requires:

```text
il2cpp_string_length
il2cpp_string_chars
```

Vector-array inspection additionally requires:

```text
il2cpp_array_length
il2cpp_array_get_byte_length
il2cpp_array_element_size
il2cpp_class_get_element_class
il2cpp_class_get_type
```

Conservative blittable reads additionally require complete type metadata including IL2CPP blittable classification and value size/alignment. These feature groups remain optional and do not make `Attach()` stricter.

## Optional type relationships and metadata

Parent, interface, nested-type, and declaring-type endpoints depend only on the specific relationship export they use. Materializing a related type additionally requires IL2CPP class image/name/namespace APIs.

`ResolvedType.GetMetadata()` and `ResolvedMethod.GetMetadata()` are likewise optional capabilities. Missing metadata exports do not make `Attach()` fail and do not affect targeted semantic resolution.

# Current Limitations

The current implementation deliberately does **not** provide:

- Unvalidated arbitrary unmanaged-struct reads; only explicitly matching blittable value types are accepted.
- Multidimensional array inspection or eager local `T[]` materialization.
- Unboxed value-type instance field addressing.
- `ThreadStatic` value resolution.
- Literal constant retrieval.
- Field writes.
- Property setter invocation.
- Indexed/parameterized property getter invocation.
- General managed method invocation.


The current implementation intentionally does not attempt to solve every IL2CPP runtime problem.

Known boundaries include:

- Windows x64 only.
- Thread-static field storage is not resolved by the normal static-field storage API.
- No general managed method invocation API; the invocation layer is intentionally limited to parameterless property getters.
- Property setters and indexed/parameterized getters remain unsupported.
- A resolved native method address does not guarantee ABI-safe invocation.
- Automatic MethodInfo detection depends on enough method evidence from the declaring type.
- Automatic Il2CppClass detection requires consumer-provided normal static-field evidence across multiple classes.
- Array payload addressing currently assumes the validated Windows x64 `Il2CppArray` vector offset `0x20`; public runtime APIs are used to cross-check array shape and payload size before that structural offset is consumed.
- Managed strings are bounded by a defensive maximum character count before local allocation.
- Some navigation endpoints depend on optional target exports.
- Semantic modeling of advanced generic/nested-type identity may be extended later.
- Layout catalogues intentionally cover explicit known structures rather than pretending every Unity/IL2CPP generation shares one layout.

---

# Complete Example

The following example demonstrates configuration, navigation, exact overload resolution, metadata inspection, type relationships, native method-code mapping, field resolution, safe reference field reading, static-field storage, enumeration, and cache invalidation. Specialized string, array, scalar, enum, and blittable reads use the same `ResolvedField` APIs documented above.

```csharp
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;

using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

// Configure known structural layouts once for this session.
resolver.SetMethodInfoLayout(
    Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);

resolver.SetFieldStorageLayout(
    Il2CppClassLayouts.Class29_2X64);

// Enumerate loaded assemblies.
IReadOnlyList<ResolvedAssembly> assemblies = resolver.GetAssemblies();
Console.WriteLine($"Assemblies: {assemblies.Count}");

// Resolve one assembly semantically.
ResolvedAssembly inputAssembly = resolver.ResolveAssembly(
    new AssemblyQuery("Unity.InputSystem"));

Console.WriteLine($"Assembly*: 0x{inputAssembly.AssemblyAddress:X}");
Console.WriteLine($"Image*:    0x{inputAssembly.ImageAddress:X}");

// Navigate to a known type without enumerating every type first.
ResolvedType inputSystem = inputAssembly.ResolveType(
    "UnityEngine.InputSystem",
    "InputSystem");

Console.WriteLine($"Class*:    0x{inputSystem.ClassAddress:X}");

// Explore only the overloads sharing one method name.
IReadOnlyList<ResolvedMethod> queueEventOverloads =
    inputSystem.GetMethods("QueueEvent");

foreach (ResolvedMethod overload in queueEventOverloads)
{
    string parameters = string.Join(", ", overload.ParameterTypeNames);
    Console.WriteLine($"{overload.ReturnTypeName} {overload.Query.Name}({parameters})");
}

// Resolve one exact overload.
ResolvedMethod queueEvent = inputSystem.ResolveMethod(
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

// Map the resolved MethodInfo to direct native executable code.
ResolvedMethodCode queueEventCode = queueEvent.ResolveCode();

Console.WriteLine($"MethodInfo*: 0x{queueEvent.MethodInfoAddress:X}");
Console.WriteLine($"Native:      0x{queueEventCode.NativeAddress:X}");
Console.WriteLine($"Section:     {queueEventCode.SectionName}");
Console.WriteLine($"Profile:     {queueEventCode.CompatibilityProfile}");

// Enumerate and resolve properties.
IReadOnlyList<ResolvedProperty> properties = inputSystem.GetProperties();
Console.WriteLine($"Properties:  {properties.Count}");

if (properties.Count > 0)
{
    ResolvedProperty property = properties[0];
    ResolvedProperty resolvedProperty = inputSystem.ResolveProperty(
        property.Query.Name,
        property.IndexParameterTypeNames.ToArray());

    Console.WriteLine($"PropertyInfo*: 0x{resolvedProperty.PropertyInfoAddress:X}");
    Console.WriteLine($"Property type:  {resolvedProperty.TypeName}");
    Console.WriteLine($"CanRead:        {resolvedProperty.CanRead}");
    Console.WriteLine($"CanWrite:       {resolvedProperty.CanWrite}");
}

// Property value reads execute managed getter code.
ResolvedProperty pollingFrequency = inputSystem.ResolveProperty("pollingFrequency");
float currentPollingFrequency = pollingFrequency.ReadStatic<float>();
Console.WriteLine($"Polling frequency: {currentPollingFrequency}");

// Resolve one field.
ResolvedField manager = inputSystem.ResolveField("s_Manager");

Console.WriteLine($"FieldInfo*:  0x{manager.FieldInfoAddress:X}");
Console.WriteLine($"Field type:  {manager.TypeName}");
Console.WriteLine($"Storage:     {manager.StorageKind}");

// Map normal static storage.
ResolvedFieldStorage managerStorage = manager.ResolveStorage();

Console.WriteLine($"Static base: 0x{managerStorage.StaticFieldsAddress:X}");
Console.WriteLine($"Static size: 0x{managerStorage.StaticFieldsSize:X}");
Console.WriteLine($"Offset:      0x{managerStorage.StaticStorageOffset:X}");
Console.WriteLine($"Address:     0x{managerStorage.StorageAddress:X}");
Console.WriteLine($"Source:      {managerStorage.ResolutionSource}");

// Read the normal static managed reference after runtime type and storage validation.
nint managerObject = manager.ReadStaticReference();
Console.WriteLine($"InputManager*: 0x{managerObject:X}");

// Navigate class relationships and inspect cached metadata explicitly.
ResolvedType? baseType = inputSystem.GetBaseType();
IReadOnlyList<ResolvedType> interfaces = inputSystem.GetInterfaces();
ResolvedTypeMetadata inputSystemMetadata = inputSystem.GetMetadata();
ResolvedMethodMetadata queueEventMetadata = queueEvent.GetMetadata();

Console.WriteLine($"Base type:    {baseType?.Query.Name}");
Console.WriteLine($"Interfaces:   {interfaces.Count}");
Console.WriteLine($"Type token:   0x{inputSystemMetadata.MetadataToken:X8}");
Console.WriteLine($"Method token: 0x{queueEventMetadata.MetadataToken:X8}");

// Explicitly invalidate runtime snapshots when required.
resolver.ClearCache();

// Old resolved entities retain snapshot properties but cannot navigate.
Console.WriteLine($"Old Class*: 0x{inputSystem.ClassAddress:X}");

// Re-resolve before performing additional navigation.
ResolvedType refreshedInputSystem = resolver.ResolveType(
    new TypeQuery(
        "Unity.InputSystem",
        "UnityEngine.InputSystem",
        "InputSystem"));
```

---

# Development and Validation

The current resolver has been exercised against a live IL2CPP target with validation covering:

- assembly enumeration and targeted assembly identity;
- type enumeration and targeted type identity;
- exact method overload matching;
- exhaustive method navigation;
- field enumeration and targeted field identity;
- MethodInfo → native code validation;
- static-field base + offset mapping;
- exact scalar and managed-reference field reads;
- static and instance parameterless property getter invocation with scalar/reference return validation;
- managed-exception capture, object assignability checks, virtual dispatch, and generation invalidation for property reads;
- managed-string decoding when a suitable live field is available;
- vector-array inspection, element typing and bounds rejection when a suitable live field is available;
- explicit rejection of scalar misuse through the blittable-structure API;
- configured layouts;
- one-shot layout overrides;
- resolver-level and navigation-level parity;
- identity-map reuse;
- repeated navigation/cache reuse;
- cache-generation invalidation, including stale field reads and stale `ResolvedArray` wrappers;
- persistence of explicit layout configuration across `ClearCache()`.

Integration tests should continue to distinguish functional assertions from timing observations. Cache correctness is primarily validated through stable identity and result reuse rather than assuming a particular wall-clock duration.

---

# Roadmap

Potential future work includes:

- Indexed/parameterized property getter invocation with explicit argument marshalling, only if required by future consumers.
- Thread-static storage resolution.
- Literal constant retrieval.
- Richer generic and nested semantic identities.
- Additional navigation and metadata endpoints.
- A separate, explicitly designed general managed invocation layer only after thread attachment, GC, exception, ABI, boxing/unboxing, and argument marshalling requirements are modeled safely.

Field writes and property setters are intentionally deferred to a later stage.
