using UnityIl2CppResolver.Native.Memory;
using UnityIl2CppResolver.Native.Modules;
using UnityIl2CppResolver.Native.PE;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Il2Cpp.Discovery;

/// <summary>
/// Detects and validates the presence of an accessible Unity IL2CPP runtime inside an attached target process.
/// This component is the entry point of the IL2CPP-specific layer: it consumes generic native process, module and PE primitives and produces an <see cref="Il2CppTarget"/> that can safely be consumed by future runtime and metadata resolution backends.
/// The current detection strategy validates <c>GameAssembly.dll</c> and a minimal set of public IL2CPP runtime exports; additional detection strategies can be introduced later without changing the resulting target model.
/// </summary>
internal sealed class Il2CppTargetDetector
{
    /// <summary>
    /// Defines the native module name used by Unity IL2CPP Windows players to host the IL2CPP runtime and compiled managed code.
    /// This identifier belongs exclusively to the IL2CPP discovery layer and is intentionally absent from the generic native module infrastructure.
    /// </summary>
    private const string GameAssemblyModuleName = "GameAssembly.dll";

    /// <summary>
    /// Defines the minimal IL2CPP runtime export fingerprint required by the current discovery strategy.
    /// These exports cover domain, assembly, image and type discovery and provide strong evidence that the loaded GameAssembly exposes a usable IL2CPP runtime API.
    /// </summary>
    private static readonly string[] RequiredRuntimeExports =
    {
        "il2cpp_domain_get",
        "il2cpp_domain_get_assemblies",
        "il2cpp_assembly_get_image",
        "il2cpp_image_get_name",
        "il2cpp_class_from_name"
    };

    /// <summary>
    /// Represents the target process inspected by this detector.
    /// The detector does not own the process lifetime and only consumes the validated process abstraction supplied by the caller.
    /// </summary>
    private readonly TargetProcess _target;

    /// <summary>
    /// Provides read-only access to the target process memory during PE and IL2CPP runtime inspection.
    /// This accessor is created once for the detector and transferred to the resulting <see cref="Il2CppTarget"/> when detection succeeds.
    /// </summary>
    private readonly ProcessMemory _memory;

    /// <summary>
    /// Provides native module discovery for the target process.
    /// The detector uses this catalog exclusively to locate the IL2CPP runtime module before delegating PE parsing to the native inspection layer.
    /// </summary>
    private readonly ModuleCatalog _modules;

    /// <summary>
    /// Initializes an IL2CPP target detector for the specified process.
    /// Native helper components required by the detection flow are created internally to guarantee that they all operate against the same target process.
    /// </summary>
    /// <param name="target">The validated Windows x64 process to inspect for an IL2CPP runtime.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    public Il2CppTargetDetector(TargetProcess target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
        _memory = new ProcessMemory(target);
        _modules = new ModuleCatalog(target);
    }

    /// <summary>
    /// Detects and validates the IL2CPP runtime associated with the target process.
    /// The operation locates <c>GameAssembly.dll</c>, parses its loaded PE image and validates the minimal runtime export fingerprint required by the current IL2CPP discovery strategy.
    /// </summary>
    /// <returns>A validated <see cref="Il2CppTarget"/> containing the native components required by higher-level IL2CPP resolution layers.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>GameAssembly.dll</c> is not loaded in the target process.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when <c>GameAssembly.dll</c> is not a supported PE image or does not expose the required IL2CPP runtime fingerprint.
    /// </exception>
    public Il2CppTarget Detect()
    {
        _target.ThrowIfExited();

        ProcessModuleInfo? gameAssembly = _modules.Find(GameAssemblyModuleName);

        if (gameAssembly is null)
            throw new InvalidOperationException($"Module '{GameAssemblyModuleName}' is not loaded in process {_target.ProcessId}.");

        PeImage gameAssemblyImage = PeImage.Read(_memory, gameAssembly);

        ValidateRuntimeFingerprint(gameAssemblyImage);

        return new Il2CppTarget(_target, _memory, gameAssembly, gameAssemblyImage);
    }

    /// <summary>
    /// Validates that the loaded GameAssembly image exposes the minimal public IL2CPP runtime API expected by the current discovery strategy.
    /// Required exports must exist as direct runtime addresses because forwarded entries cannot currently be consumed as IL2CPP entry points by higher-level resolver components.
    /// </summary>
    /// <param name="gameAssemblyImage">The parsed PE image representing the loaded <c>GameAssembly.dll</c> module.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when a required IL2CPP runtime export is absent or represented as a forwarded export.
    /// </exception>
    private static void ValidateRuntimeFingerprint(PeImage gameAssemblyImage)
    {
        foreach (string exportName in RequiredRuntimeExports)
        {
            PeExport? export = gameAssemblyImage.FindExport(exportName);

            if (export is null)
                throw new InvalidDataException($"GameAssembly does not expose the required IL2CPP runtime export '{exportName}'.");

            if (export.IsForwarded || export.Address is null)
                throw new InvalidDataException($"IL2CPP runtime export '{exportName}' does not resolve to a direct address inside GameAssembly.");
        }
    }
}