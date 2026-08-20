namespace UnityIl2CppResolver.Il2Cpp.Layouts;

/// <summary>
/// Describes the version-sensitive native offsets required to inspect normal static-field storage inside an IL2CPP <c>Il2CppClass</c> structure.
/// Instances are immutable structural definitions and contain no runtime detection, process-memory access or target-specific state.
/// </summary>
public sealed class Il2CppClassLayout
{
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
    /// </summary>
    /// <param name="name">The diagnostic name used to identify the layout in resolution results.</param>
    /// <param name="staticFieldsPointerOffset">The byte offset of <c>static_fields</c> inside <c>Il2CppClass</c>.</param>
    /// <param name="staticFieldsSizeOffset">The byte offset of <c>static_fields_size</c> inside <c>Il2CppClass</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or an offset does not satisfy its native alignment requirement.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either structural offset is negative.</exception>
    public Il2CppClassLayout(string name, int staticFieldsPointerOffset, int staticFieldsSizeOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(staticFieldsPointerOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(staticFieldsSizeOffset);

        if (staticFieldsPointerOffset % IntPtr.Size != 0)
            throw new ArgumentException("The static-fields pointer offset must be pointer-aligned.", nameof(staticFieldsPointerOffset));

        if (staticFieldsSizeOffset % sizeof(uint) != 0)
            throw new ArgumentException("The static-fields size offset must be aligned to a 32-bit value.", nameof(staticFieldsSizeOffset));

        Name = name.Trim();
        StaticFieldsPointerOffset = staticFieldsPointerOffset;
        StaticFieldsSizeOffset = staticFieldsSizeOffset;
    }
}
