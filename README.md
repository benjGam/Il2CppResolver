# Unity IL2CPP Resolver

Windows x64 semantic IL2CPP resolver with explicit compatibility layouts, optional automatic layout detection and session-scoped runtime caching.

## Public API

Normal consumers only need these namespaces:

```csharp
using UnityIl2CppResolver.Il2Cpp;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Queries;
using UnityIl2CppResolver.Il2Cpp.Results;
```

## Typical usage with known layouts

```csharp
using Il2CppResolver resolver = Il2CppResolver.Attach(processId);

resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);

MethodQuery methodQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "QueueEvent",
    "UnityEngine.InputSystem.LowLevel.InputEventPtr");

FieldQuery fieldQuery = new(
    "Unity.InputSystem",
    "UnityEngine.InputSystem",
    "InputSystem",
    "s_Manager");

ResolvedMethodCode methodCode = resolver.ResolveMethodCode(methodQuery);
ResolvedFieldStorage fieldStorage = resolver.ResolveFieldStorage(fieldQuery);
```

## Layout configuration

`SetMethodInfoLayout` and `SetFieldStorageLayout` select a layout globally for the resolver session. The selection survives `ClearCache()`.

```csharp
resolver.SetMethodInfoLayout(Il2CppMethodInfoLayouts.DirectMethodPointerFirstX64);
resolver.SetFieldStorageLayout(Il2CppClassLayouts.Class29_2X64);
```

`ResetMethodInfoLayout` and `ResetFieldStorageLayout` remove the explicit selection and restore automatic behavior.

One-shot overloads remain available and do not modify global session configuration:

```csharp
ResolvedMethodCode code = resolver.ResolveMethodCode(query, customMethodLayout);
ResolvedFieldStorage storage = resolver.ResolveFieldStorage(fieldQuery, customClassLayout);
```

Custom layouts can participate in automatic detection without becoming the selected global layout:

```csharp
resolver.RegisterMethodInfoLayout(customMethodLayout);
resolver.RegisterFieldStorageLayout(customClassLayout);
```

The registry rejects duplicate structural identities so automatic detection cannot become ambiguous only because two profiles use different names for the same offsets.

## Static field storage selection

Default `ResolveFieldStorage(query)` follows this order:

1. Explicit layout selected with `SetFieldStorageLayout`.
2. Public IL2CPP static-field storage API when the target exports both required functions.
3. A class layout previously established through automatic detection.
4. Failure with a configuration diagnostic.

Supplying an explicit layout always forces the structural layout path.

## Cache semantics

The resolver keeps session-scoped snapshots for:

- the active IL2CPP domain;
- loaded assemblies;
- methods and fields per `Il2CppClass*`;
- inspected method and field descriptions;
- IL2CPP type names;
- semantic resolution results;
- native method-code mappings;
- static-field storage mappings.

`ClearCache()` invalidates all runtime snapshots, semantic results and automatically detected layouts. Explicitly selected layouts and registered layout candidates are preserved.

## Architecture

```text
Il2Cpp/
├── Il2CppResolver.cs       public facade
├── Queries/                public semantic queries
├── Results/                public resolution results
├── Layouts/                public layout definitions/catalogues + internal registry
├── Discovery/              internal target discovery
├── Runtime/
│   ├── Model/              internal runtime models
│   └── Catalog/            session-scoped runtime snapshots
├── Detection/              internal compatibility detectors
├── Mapping/                internal native/storage mapping
└── Resolution/             internal semantic backend, cache and session orchestration

Native/
├── Process/
├── Memory/
├── Modules/
├── PE/
└── Remote/
```

## Runtime constraints

The current implementation requires:

- Windows;
- a 64-bit resolver host process;
- a native Windows x64 IL2CPP target;
- an accessible `GameAssembly.dll` exposing the required IL2CPP runtime API.

Thread-static field storage is intentionally not handled by the normal static-field storage API.
