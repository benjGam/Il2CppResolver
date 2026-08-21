# Unity IL2CPP Resolver

Semantic IL2CPP resolution and runtime navigation for live Windows x64 Unity processes.

```text
Assembly → Type → Method → Native Code
                └→ Field  → Static Storage
```

`Il2CppResolver` resolves managed identities such as assemblies, types, methods, and fields against a live IL2CPP runtime. It keeps semantic resolution separate from version-sensitive native layout interpretation, and exposes session-bound navigation endpoints for exploring the resolved runtime without leaking the internal process, PE, remote-call, or cache infrastructure.

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
- [Methods](#methods)
  - [Exact Method Resolution](#exact-method-resolution)
  - [Enumerating Methods by Name](#enumerating-methods-by-name)
  - [Enumerating All Methods](#enumerating-all-methods)
- [Native Method Code](#native-method-code)
- [Fields](#fields)
  - [Targeted Field Resolution](#targeted-field-resolution)
  - [Enumerating Fields](#enumerating-fields)
  - [Field Storage Kinds](#field-storage-kinds)
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
  - [Optional runtime static-field storage](#optional-runtime-static-field-storage)
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
- Resolve fields and classify their runtime storage kind.
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
- Runtime-API static-field storage requires both optional static-storage exports.
- Targeted semantic resolution can continue to work when those optional capabilities are absent.

---

## Public Namespaces

Normal consumers should only need:

```csharp
using UnityIl2CppResolver.Il2Cpp;
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

ResolvedField managerField = inputSystemType.ResolveField("s_Manager");
ResolvedFieldStorage managerStorage = managerField.ResolveStorage();

Console.WriteLine($"MethodInfo*:      0x{queueEvent.MethodInfoAddress:X}");
Console.WriteLine($"Native code:      0x{queueEventCode.NativeAddress:X}");
Console.WriteLine($"FieldInfo*:       0x{managerField.FieldInfoAddress:X}");
Console.WriteLine($"Static storage:   0x{managerStorage.StorageAddress:X}");
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

---

## Resolved Runtime Entities

The main resolved entities are:

```text
ResolvedAssembly
ResolvedType
ResolvedMethod
ResolvedField
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
    ├── GetFields()
    └── ResolveField(...)

ResolvedMethod
    └── ResolveCode(...)

ResolvedField
    └── ResolveStorage(...)
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

This prevents a previously resolved `Il2CppClass*`, `MethodInfo*`, `FieldInfo*`, or image identity from being silently reused after the caller explicitly requested runtime cache invalidation.

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
| `KeyNotFoundException` | A requested semantic assembly, type, method, or field was not found. |
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
- building a method or field name index for a class;
- materializing complete signatures for enumerated methods;
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

The current resolver primarily uses reads and short-lived remote execution required to call IL2CPP runtime APIs.

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

## Optional runtime static-field storage

Layout-independent runtime static storage requires:

```text
il2cpp_class_get_static_field_data
il2cpp_class_get_data_size
```

When these two exports are unavailable, explicit or previously detected class-layout resolution remains available.

---

# Current Limitations

The current implementation intentionally does not attempt to solve every IL2CPP runtime problem.

Known boundaries include:

- Windows x64 only.
- Thread-static field storage is not resolved by the normal static-field storage API.
- No generic public API for reading field values yet.
- No general managed method invocation API.
- A resolved native method address does not guarantee ABI-safe invocation.
- Automatic MethodInfo detection depends on enough method evidence from the declaring type.
- Automatic Il2CppClass detection requires consumer-provided normal static-field evidence across multiple classes.
- Some navigation endpoints depend on optional target exports.
- Semantic modeling of advanced generic/nested-type identity may be extended later.
- Layout catalogues intentionally cover explicit known structures rather than pretending every Unity/IL2CPP generation shares one layout.

---

# Complete Example

The following example demonstrates configuration, navigation, exact overload resolution, native method-code mapping, field resolution, static-field storage, enumeration, and cache invalidation.

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
- configured layouts;
- one-shot layout overrides;
- resolver-level and navigation-level parity;
- identity-map reuse;
- repeated navigation/cache reuse;
- cache-generation invalidation;
- persistence of explicit layout configuration across `ClearCache()`.

Integration tests should continue to distinguish functional assertions from timing observations. Cache correctness is primarily validated through stable identity and result reuse rather than assuming a particular wall-clock duration.

---

# Roadmap

Potential future work includes:

- public field-value reading APIs;
- additional runtime-backed capabilities that reduce structural layout dependency;
- additional compatibility profiles;
- more navigation endpoints;
- improved generic and nested semantic identity modeling;
- additional automated tests for negative layout and capability scenarios.

The current architecture intentionally keeps those features separate from the semantic resolver core so they can be added without coupling query identity to version-sensitive native runtime structures.
