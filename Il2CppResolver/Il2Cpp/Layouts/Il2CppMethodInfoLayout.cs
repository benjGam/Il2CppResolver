namespace UnityIl2CppResolver.Il2Cpp.Layouts;

/// <summary>
/// Describes the version-sensitive native offset required to locate the direct executable method pointer inside an IL2CPP <c>MethodInfo</c> structure.
/// Instances are immutable structural definitions and contain no runtime detection or process-memory access.
/// </summary>
public sealed class Il2CppMethodInfoLayout
{
    /// <summary>
    /// Gets the diagnostic name identifying this structural compatibility profile.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the byte offset of the direct native <c>methodPointer</c> inside <c>MethodInfo</c>.
    /// </summary>
    public int DirectMethodPointerOffset { get; }

    /// <summary>
    /// Initializes an explicit IL2CPP <c>MethodInfo</c> layout definition.
    /// </summary>
    /// <param name="name">The diagnostic name used to identify the layout in native method resolution results.</param>
    /// <param name="directMethodPointerOffset">The byte offset of the direct native <c>methodPointer</c> inside <c>MethodInfo</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or <paramref name="directMethodPointerOffset"/> is not pointer-aligned.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="directMethodPointerOffset"/> is negative.</exception>
    public Il2CppMethodInfoLayout(string name, int directMethodPointerOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(directMethodPointerOffset);

        if (directMethodPointerOffset % IntPtr.Size != 0)
            throw new ArgumentException("The direct method pointer offset must be pointer-aligned.", nameof(directMethodPointerOffset));

        Name = name.Trim();
        DirectMethodPointerOffset = directMethodPointerOffset;
    }
}
