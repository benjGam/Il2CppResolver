# Technical Documentation

This document contains implementation-oriented documentation for maintainers and advanced contributors.

For the supported consumer API, endpoint usage, C# type mapping, examples, and complete workflows, see [README.md](README.md).

## Contents

- [Core Concepts](#core-concepts)
- [Resolver Lifetime](#resolver-lifetime)
- [Layout Configuration](#layout-configuration)
- [Automatic Layout Detection](#automatic-layout-detection)
- [Native Method Code](#native-method-code)
- [Static Field Storage](#static-field-storage)
- [Navigation API](#navigation-api)
- [Caching and Runtime Catalogues](#caching-and-runtime-catalogues)
- [Targeted Resolution vs. Enumeration](#targeted-resolution-vs-enumeration)
- [Runtime Identity Map](#runtime-identity-map)
- [ClearCache and Cache Generations](#clearcache-and-cache-generations)
- [Concurrency Model](#concurrency-model)
- [Performance Characteristics](#performance-characteristics)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Runtime Execution Model](#runtime-execution-model)
- [Safety Model](#safety-model)
- [Required and Optional Runtime Capabilities](#required-and-optional-runtime-capabilities)
- [Development and Validation](#development-and-validation)

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

They expose immutable runtime identity information and are bound to the resolver session and cache generation that produced them.

They also provide navigation and value endpoints:

```text
ResolvedAssembly
    ├── GetTypes()
    └── ResolveType(...)

ResolvedType
    ├── GetMethods() / GetMethods(name) / ResolveMethod(...)
    ├── GetProperties() / GetProperties(name) / ResolveProperty(...)
    ├── GetFields() / ResolveField(...)
    ├── GetBaseType() / GetInterfaces()
    ├── GetNestedTypes() / GetDeclaringType()
    └── GetMetadata()

ResolvedMethod
    ├── ResolveCode(...)
    └── GetMetadata()

ResolvedProperty
    ├── Getter → ResolvedMethod?
    ├── Setter → ResolvedMethod?
    ├── Read<T>() / ReadStatic<T>()
    ├── ReadEnum<TEnum>() / ReadStaticEnum<TEnum>()
    ├── ReadReference() / ReadStaticReference()
    ├── ReadString() / ReadStaticString()
    ├── ReadArray() / ReadStaticArray()
    └── ReadBlittable<T>() / ReadStaticBlittable<T>()

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

The runtime logic remains centralized in the owning resolver session. Resolved entities are immutable, session-bound navigation handles rather than independent runtime backends.

Field and property value endpoints deliberately use different mechanisms:

```text
ResolvedField
    → validated storage/address calculation
    → validated remote memory read

ResolvedProperty
    → validated getter signature
    → controlled managed invocation
    → rooted and validated result decoding
```

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

The public API keeps semantic navigation separate from native mapping, memory inspection, and managed invocation:

```text
Il2CppResolver
      ↓
ResolvedAssembly
      ↓
ResolvedType
      ├──────────────────┬────────────────────┐
      ↓                  ↓                    ↓
ResolvedMethod     ResolvedProperty      ResolvedField
      │             │          │           │        │
      ↓             ↓          ↓           ↓        ↓
ResolvedMethodCode Accessors   Read*   ResolveStorage Read*
                               │                       │
                               ↓                       ↓
                    controlled getter            validated memory
                       invocation                    read
```

A typical navigation flow is:

```csharp
ResolvedAssembly assembly = resolver.ResolveAssembly(new AssemblyQuery("Unity.InputSystem"));
ResolvedType type = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

ResolvedMethod method = type.ResolveMethod("QueueEvent", "UnityEngine.InputSystem.LowLevel.InputEventPtr");
ResolvedMethodCode code = method.ResolveCode();

ResolvedField field = type.ResolveField("s_Manager");
ResolvedFieldStorage storage = field.ResolveStorage();
nint managerObject = field.ReadStaticReference();

ResolvedProperty property = type.ResolveProperty("pollingFrequency");
float pollingFrequency = property.ReadStatic<float>();
```

Navigation does not duplicate runtime state. Every resolved entity delegates through the same session and reuses the same catalogues, identity maps, generation checks, and lifetime rules.

# Caching and Runtime Catalogues

The resolver maintains session-scoped local state to reduce expensive remote runtime calls and preserve stable public identity within one cache generation.

The current cache/catalogue layers include:

- active IL2CPP domain snapshot;
- loaded assembly snapshot and assembly lookup indexes;
- image type catalogues;
- raw method, property, and field addresses per `Il2CppClass*`;
- member-name indexes;
- lazily inspected method, property, and field descriptions;
- IL2CPP type-name cache;
- runtime type descriptors and class metadata;
- array element type and element-size descriptors;
- semantic query results;
- runtime identity maps;
- layout-sensitive native method mappings;
- layout-sensitive static-field storage mappings;
- runtime-API static-field storage mappings.

Managed objects returned by property getters use a separate lifetime mechanism rather than the semantic cache itself. Non-null raw-reference and `ResolvedArray` results can retain strong IL2CPP GC handles in the session so their remote object identity remains valid after the temporary invocation thread detaches. Those retained roots are released by `ClearCache()` and resolver disposal.

The design intentionally optimizes local reuse and explicit session ownership without introducing a persistent remote worker.

# Targeted Resolution vs. Enumeration

Use targeted resolution when the identity is already known:

```csharp
resolver.ResolveAssembly(...)
resolver.ResolveType(...)
type.ResolveMethod(...)
type.ResolveProperty(...)
type.ResolveField(...)
```

Use enumeration for exploration:

```csharp
resolver.GetAssemblies()
assembly.GetTypes()
type.GetMethods()
type.GetMethods("Name")
type.GetProperties()
type.GetProperties("Name")
type.GetFields()
```

The intended cost model is:

```text
Targeted ResolveX
    → narrow the search as early as possible

GetXs
    → explicitly materialize an exploratory snapshot
```

For example, `ResolveMethod` does not call `GetMethods()` and inspect every signature. It uses the class member name index first and inspects complete signatures only for matching-name candidates. Property resolution follows the same targeted-name-first model before accessor signatures are materialized.

# Runtime Identity Map

Within one cache generation, runtime identities are reused by native address.

The session maintains identity maps for concepts such as:

```text
Il2CppImage*   → ResolvedAssembly
Il2CppClass*   → ResolvedType
MethodInfo*    → ResolvedMethod
PropertyInfo*  → ResolvedProperty
FieldInfo*     → ResolvedField
```

Therefore, targeted resolution and navigation can converge on the same public object instance:

```csharp
ResolvedType first = resolver.ResolveType(typeQuery);
ResolvedType second = assembly.ResolveType("UnityEngine.InputSystem", "InputSystem");

bool sameObject = ReferenceEquals(first, second);
```

Within the same generation, `sameObject` is expected to be `true` when both paths resolve the same runtime entity. The same rule applies when a property accessor is reached through `ResolvedProperty.Getter` / `Setter` and later resolved independently as a method.

# ClearCache and Cache Generations

```csharp
resolver.ClearCache();
```

Before invalidating runtime snapshots, `ClearCache()` releases strong GC handles retained for managed reference/array results returned by property getters. It then invalidates:

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

Resolver disposal follows the same ownership rule for retained managed results: roots still owned by the session are released before the session and process attachment are torn down. If a remote cleanup operation cannot be proven safe, the resolver follows its conservative abandonment policy rather than retrying a potentially unsafe free.

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
          ┌────────────────────┼─────────────────────┐
          │                    │                     │
  ResolutionCache       RuntimeCatalogues      LayoutRegistry
          │                    │                     │
          └──────────────┬─────┴──────────────┬──────┘
                         │                    │
             RuntimeResolutionBackend   Il2CppGetterInvoker
                         │                    │
                         └──────────┬─────────┘
                                    │
                               Il2CppRuntime
                                    │
                               RemoteCall
```

Semantic resolution remains separate from native method mapping:

```text
ResolvedMethod
      ↓
Il2CppMethodPointerResolver
      ↓
ResolvedMethodCode
```

Properties reuse method identity for their accessors, but value reads follow a controlled invocation path:

```text
ResolvedProperty
    ├── Getter → ResolvedMethod
    ├── Setter → ResolvedMethod
    │
    └── Read*
          ↓
    Il2CppGetterInvoker
          ↓
    validate signature / static-instance contract
          ↓
    root instance when required
          ↓
    runtime class + assignability + virtual dispatch
          ↓
    il2cpp_runtime_invoke
          ↓
    root result before thread detach
          ↓
    decode locally or retain result root
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
ResolvedType   → Il2CppTypeCatalog           → parent / interfaces / nested / declaring / metadata
ResolvedMethod → Il2CppMethodMetadataCatalog → method metadata
```

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
│   ├── Model/               internal raw runtime and invocation descriptions
│   └── Catalog/             session-scoped runtime snapshots and indexes
├── Detection/               internal layout detectors
├── Mapping/                 internal method-code and field-storage mappers
├── Invocation/              controlled parameterless property-getter invocation and GC-root lifetime
├── Values/                  internal field/type validation and safe value decoding
└── Resolution/              semantic backend, cache, identity map and session orchestration
```

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

Managed object lifetime is explicit around invocation:

```text
caller-provided instance
    → temporary strong GC root
    → rooted target used for validation and virtual dispatch
    → managed invocation
    → temporary root released when completion is proven

non-null getter result
    → strong GC root created before invocation-thread detach
    → scalar/string/blittable result: decode then release
    → raw reference/ResolvedArray: retain until ClearCache()/Dispose()
```

`Il2CppGCHandle` is treated as an opaque pointer-sized native value (`nuint`) throughout the resolver. No invocation path truncates it to a 32-bit value.

## Remote timeout safety

A critical invariant is:

> Remote memory or managed roots must not be released while a remote thread may still reference them.

When thread termination cannot be proven because of a timeout, failed wait, or unexpected synchronization state, remote allocations or temporary roots that may still be referenced by that thread are intentionally abandoned rather than freed.

This can leak target resources until the target process terminates, but avoids remote use-after-free or invalid GC-handle cleanup.

For this layer:

```text
controlled leak
    >
unsafe remote free
```

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

The current property getter gate depends on runtime type classification plus the following base invocation exports:

```text
il2cpp_type_get_type
il2cpp_class_from_type
il2cpp_class_is_valuetype
il2cpp_class_is_enum

il2cpp_runtime_invoke
il2cpp_object_unbox
il2cpp_thread_attach
il2cpp_thread_detach
il2cpp_method_is_instance
il2cpp_method_is_generic
il2cpp_object_get_class
il2cpp_object_get_virtual_method
il2cpp_class_is_assignable_from
il2cpp_gchandle_new
il2cpp_gchandle_free
```

Instance getters additionally require the complete managed-object rooting capability:

```text
il2cpp_gchandle_get_target
```

The string and array return endpoints also require their respective optional decoding capabilities documented below. Missing invocation support does not affect property navigation or normal semantic resolution; only the affected property value read throws `NotSupportedException`.

Returned non-null managed objects are strongly rooted before the temporary invocation thread detaches. Instance getters first create a temporary strong root for the supplied object and recover the protected target through `il2cpp_gchandle_get_target`; runtime class validation, assignability checks, virtual dispatch, and managed invocation then use that rooted object. Static getter invocation does not require an instance root.

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

# Development and Validation

The current resolver has been exercised against a live IL2CPP target with validation covering:

- assembly enumeration and targeted assembly identity;
- type enumeration and targeted type identity;
- exact method overload matching and exhaustive method navigation;
- property enumeration, accessor identity reuse, and targeted property identity;
- field enumeration and targeted field identity;
- MethodInfo → native code validation;
- static-field base + offset mapping;
- exact scalar and managed-reference field reads;
- managed-string decoding when a suitable live field is available;
- vector-array inspection, element typing, and bounds rejection when a suitable live field is available;
- explicit rejection of scalar misuse through the blittable-structure API;
- static parameterless property getter invocation with scalar and managed-reference results;
- instance parameterless property getter invocation through temporary strong instance rooting, runtime assignability validation, and virtual dispatch;
- strong result rooting before invocation-thread detach and retained reference lifetime across normal navigation;
- managed-exception capture through `Il2CppInvocationException`;
- configured layouts and one-shot layout overrides;
- resolver-level and navigation-level parity;
- identity-map reuse for assemblies, types, methods, properties, and fields;
- repeated navigation/cache reuse;
- cache-generation invalidation, including stale field reads, stale property getter invocation, stale relationships/metadata, and stale `ResolvedArray` wrappers;
- release of session-retained managed result roots during cache invalidation;
- persistence of explicit layout configuration across `ClearCache()`;
- rematerialization of metadata, relationships, and value-reading behavior in the new generation.

Integration tests distinguish functional assertions from timing observations. Cache correctness is validated through stable identity, result reuse, explicit stale-generation rejection, and successful rematerialization rather than assuming a particular wall-clock duration.
