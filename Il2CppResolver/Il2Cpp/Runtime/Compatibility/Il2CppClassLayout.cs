namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Describes the version-sensitive native offsets required to inspect normal static-field storage inside an IL2CPP <c>Il2CppClass</c> structure.
/// Consumers may select one of the known compatibility profiles or provide an explicit custom layout when the target runtime structure is already known.
/// The layout contains structural information only and performs no runtime detection or memory access itself.
/// </summary>
public sealed class Il2CppClassLayout
{
    /// <summary>
    /// Gets the Windows x64 IL2CPP 29.1 class layout in which <c>static_fields</c> resides at offset <c>0xB8</c> and <c>static_fields_size</c> at offset <c>0x108</c>.
    /// </summary>
    public static Il2CppClassLayout Class29_1X64 { get; } = new("Class29_1X64", 0xB8, 0x108);

    /// <summary>
    /// Gets the Windows x64 IL2CPP 29.2 class layout in which <c>static_fields</c> resides at offset <c>0xB8</c> and <c>static_fields_size</c> at offset <c>0x10C</c>.
    /// </summary>
    public static Il2CppClassLayout Class29_2X64 { get; } = new("Class29_2X64", 0xB8, 0x10C);

    /// <summary>
    /// Gets the diagnostic name identifying this structural compatibility profile.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the byte offset of the <c>static_fields</c> pointer inside <c>Il2CppClass</c>.
    /// </summary>
    public int StaticFieldsPointerOffset { get; }

    /// <summary>
    /// Gets the byte offset of the 32-bit <c>static_fields_size</c> value inside <c>Il2CppClass</c>.
    /// </summary>
    public int StaticFieldsSizeOffset { get; }

    /// <summary>
    /// Initializes an explicit IL2CPP class-layout definition.
    /// This constructor allows consumers to provide target-specific structure offsets when no built-in compatibility profile matches the target runtime.
    /// </summary>
    /// <param name="name">The diagnostic name used to identify the custom layout in resolution results.</param>
    /// <param name="staticFieldsPointerOffset">The byte offset of <c>static_fields</c> inside <c>Il2CppClass</c>.</param>
    /// <param name="staticFieldsSizeOffset">The byte offset of <c>static_fields_size</c> inside <c>Il2CppClass</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or when an offset does not satisfy its native alignment requirement.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either structural offset is negative.
    /// </exception>
    public Il2CppClassLayout(string name, int staticFieldsPointerOffset, int staticFieldsSizeOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(staticFieldsPointerOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(staticFieldsSizeOffset);

        if (staticFieldsPointerOffset % IntPtr.Size != 0)
            throw new ArgumentException("The static-fields pointer offset must be pointer-aligned.", nameof(staticFieldsPointerOffset));

        if (staticFieldsSizeOffset % sizeof(uint) != 0)
            throw new ArgumentException("The static-fields size offset must be aligned to a 32-bit value.", nameof(staticFieldsSizeOffset));

        Name = name;
        StaticFieldsPointerOffset = staticFieldsPointerOffset;
        StaticFieldsSizeOffset = staticFieldsSizeOffset;
    }
}