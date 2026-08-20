using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Il2Cpp.Results;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Mapping;

/// <summary>
/// Maps a semantically resolved IL2CPP <c>MethodInfo</c> to its direct native implementation address using an explicit runtime-layout compatibility profile.
/// The resolver validates every candidate against the loaded <c>GameAssembly.dll</c> image and requires the address to belong to an executable PE section before exposing it as callable native code.
/// This component isolates version-sensitive structure inspection from both semantic resolution and generic process-memory infrastructure.
/// </summary>
internal sealed class Il2CppMethodPointerResolver
{
    /// <summary>
    /// Represents the validated IL2CPP target whose runtime structures and native image are inspected.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Defines the explicit <c>MethodInfo</c> structural layout used to locate the direct native method pointer.
    /// </summary>
    private readonly Il2CppMethodInfoLayout _layout;

    /// <summary>
    /// Initializes native method-pointer mapping for the specified IL2CPP target and structural compatibility profile.
    /// </summary>
    /// <param name="target">The validated IL2CPP target containing the resolved runtime method.</param>
    /// <param name="layout">The explicit <c>MethodInfo</c> layout used to locate the direct native method pointer.</param>
    public Il2CppMethodPointerResolver(Il2CppTarget target, Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);

        _target = target;
        _layout = layout;
    }

    /// <summary>
    /// Resolves and validates the direct native implementation associated with a semantic IL2CPP method.
    /// The candidate pointer is read from the layout-defined <c>MethodInfo</c> offset and is accepted only when it belongs to an executable section of the target <c>GameAssembly.dll</c>.
    /// </summary>
    /// <param name="method">The semantically resolved IL2CPP method whose native implementation should be mapped.</param>
    /// <returns>A validated native-code mapping containing the executable address and the compatibility evidence used to obtain it.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="method"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the layout produces a null pointer, an address outside <c>GameAssembly.dll</c> or an address that does not belong to an executable PE section.
    /// </exception>
    public ResolvedMethodCode Resolve(ResolvedMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);

        nint pointerAddress = checked((nint)(method.MethodInfoAddress.ToInt64() + _layout.DirectMethodPointerOffset));
        nint nativeAddress = _target.Memory.ReadPointer(pointerAddress);

        if (nativeAddress == 0)
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' produced a null direct method pointer for MethodInfo 0x{method.MethodInfoAddress:X}.");

        PeImage image = _target.GameAssemblyImage;

        if (!image.ContainsAddress(nativeAddress))
            throw new InvalidDataException($"Compatibility profile '{_layout.Name}' produced native address 0x{nativeAddress:X} for MethodInfo 0x{method.MethodInfoAddress:X}, but the address is outside GameAssembly.");

        PeSection? section = image.FindSectionContainingAddress(nativeAddress);

        if (section is null)
            throw new InvalidDataException($"Native method address 0x{nativeAddress:X} belongs to GameAssembly but cannot be associated with a parsed PE section.");

        if (!section.IsExecutable)
            throw new InvalidDataException($"Native method address 0x{nativeAddress:X} resolves to non-executable GameAssembly section '{section.Name}'.");

        return new ResolvedMethodCode(method, nativeAddress, section.Name, _layout.Name, _layout.DirectMethodPointerOffset);
    }
}