namespace UnityIl2CppResolver.Il2Cpp.Layouts;

/// <summary>
/// Exposes the built-in immutable <see cref="Il2CppClassLayout"/> profiles known by the resolver.
/// This catalogue contains definitions only; session registration and selection are handled independently by the resolver configuration API.
/// </summary>
public static class Il2CppClassLayouts
{
    /// <summary>
    /// Gets the Windows x64 IL2CPP 29.1 structural profile with <c>static_fields</c> at <c>0xB8</c> and <c>static_fields_size</c> at <c>0x108</c>.
    /// </summary>
    public static Il2CppClassLayout Class29_1X64 { get; } = new("Class29_1X64", 0xB8, 0x108);

    /// <summary>
    /// Gets the Windows x64 IL2CPP 29.2 structural profile with <c>static_fields</c> at <c>0xB8</c> and <c>static_fields_size</c> at <c>0x10C</c>.
    /// </summary>
    public static Il2CppClassLayout Class29_2X64 { get; } = new("Class29_2X64", 0xB8, 0x10C);

    /// <summary>
    /// Gets every built-in class-layout profile registered in new resolver sessions by default.
    /// </summary>
    public static IReadOnlyList<Il2CppClassLayout> BuiltIn { get; } = Array.AsReadOnly(new[] { Class29_1X64, Class29_2X64 });
}
