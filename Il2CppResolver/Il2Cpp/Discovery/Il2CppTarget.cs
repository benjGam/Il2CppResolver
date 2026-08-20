using UnityIl2CppResolver.Native.Memory;
using UnityIl2CppResolver.Native.Modules;
using UnityIl2CppResolver.Native.PE;
using UnityIl2CppResolver.Native.Process;

namespace UnityIl2CppResolver.Il2Cpp.Discovery;

/// <summary>
/// Represents a validated Unity IL2CPP target discovered inside an attached process.
/// This class establishes the bridge between the generic native inspection layer and the IL2CPP-specific resolver layers.
/// It exposes the validated GameAssembly image and the native process primitives required by future metadata and runtime resolution backends without performing any semantic resolution itself.
/// </summary>
public sealed class Il2CppTarget
{
    /// <summary>
    /// Gets the validated target process containing the discovered IL2CPP runtime.
    /// The process remains the owner of the native process handle and defines the lifetime of every component associated with this target.
    /// </summary>
    public TargetProcess Process { get; }

    /// <summary>
    /// Gets the read-only memory accessor associated with the target process.
    /// IL2CPP discovery and resolution components use this accessor to inspect runtime structures without directly depending on Win32 memory APIs.
    /// </summary>
    public ProcessMemory Memory { get; }

    /// <summary>
    /// Gets the native module information associated with the loaded <c>GameAssembly.dll</c> image.
    /// This module defines the runtime address range containing the native IL2CPP implementation and compiled managed code.
    /// </summary>
    public ProcessModuleInfo GameAssembly { get; }

    /// <summary>
    /// Gets the parsed PE representation of the loaded <c>GameAssembly.dll</c> image.
    /// Higher-level IL2CPP components use this image to inspect sections, exports and runtime addresses without parsing PE structures themselves.
    /// </summary>
    public PeImage GameAssemblyImage { get; }

    /// <summary>
    /// Initializes an immutable IL2CPP target from native components that have already been discovered and validated.
    /// Instances are created by <see cref="Il2CppTargetDetector"/> so higher-level components can rely on the target invariants established during discovery.
    /// </summary>
    /// <param name="process">The validated process containing the IL2CPP runtime.</param>
    /// <param name="memory">The read-only memory accessor associated with the target process.</param>
    /// <param name="gameAssembly">The loaded <c>GameAssembly.dll</c> module information.</param>
    /// <param name="gameAssemblyImage">The parsed and validated PE representation of <c>GameAssembly.dll</c>.</param>
    internal Il2CppTarget(TargetProcess process, ProcessMemory memory, ProcessModuleInfo gameAssembly, PeImage gameAssemblyImage)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(gameAssembly);
        ArgumentNullException.ThrowIfNull(gameAssemblyImage);

        Process = process;
        Memory = memory;
        GameAssembly = gameAssembly;
        GameAssemblyImage = gameAssemblyImage;
    }
}