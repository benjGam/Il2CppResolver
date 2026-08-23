namespace UnityIl2CppResolver.Il2Cpp.Layouts;

/// <summary>
/// Exposes the built-in immutable <see cref="Il2CppMethodInfoLayout"/> profiles known by the resolver.
/// This catalogue contains definitions only; session registration and selection are handled independently by the resolver configuration API.
/// </summary>
public static class Il2CppMethodInfoLayouts
{
    /// <summary>
    /// Gets the Windows x64 IL2CPP layout in which the direct native <c>methodPointer</c> occupies the first pointer-sized field of <c>MethodInfo</c>.
    /// </summary>
    public static Il2CppMethodInfoLayout DirectMethodPointerFirstX64 { get; } = new("DirectMethodPointerFirstX64", 0x0);

    /// <summary>
    /// Gets every built-in method-layout profile registered in new resolver sessions by default.
    /// </summary>
    public static IReadOnlyList<Il2CppMethodInfoLayout> BuiltIn { get; } = Array.AsReadOnly(new[] { DirectMethodPointerFirstX64 });
}
