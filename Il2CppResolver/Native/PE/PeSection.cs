namespace UnityIl2CppResolver.Native.PE;

/// <summary>
/// Describes a section belonging to a Portable Executable image loaded in the target process.
/// This immutable model belongs to the native PE inspection layer and exposes the virtual layout and characteristics required by higher-level validation components.
/// IL2CPP and Unity-specific layers can use sections such as <c>.text</c> and <c>.rdata</c> without depending on raw PE header structures.
/// </summary>
internal sealed record PeSection
{
    /// <summary>
    /// Represents the PE section characteristic indicating that the section contains executable code.
    /// </summary>
    private const uint MemoryExecuteCharacteristic = 0x20000000;

    /// <summary>
    /// Gets a value indicating whether the section is marked executable by the loaded PE image.
    /// Native method pointers resolved from IL2CPP runtime structures must normally belong to an executable section before they can be considered valid code addresses.
    /// </summary>
    public bool IsExecutable => (Characteristics & MemoryExecuteCharacteristic) != 0;

    /// <summary>
    /// Gets the section name stored in the PE section header, such as <c>.text</c>, <c>.rdata</c> or <c>.data</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the relative virtual address of the section from the beginning of the loaded PE image.
    /// </summary>
    public uint VirtualAddress { get; }

    /// <summary>
    /// Gets the number of bytes occupied by the section when loaded in memory.
    /// </summary>
    public uint VirtualSize { get; }

    /// <summary>
    /// Gets the size of the initialized section data stored in the original PE file.
    /// This value is retained for PE inspection but does not represent the authoritative runtime size of the loaded section.
    /// </summary>
    public uint RawDataSize { get; }

    /// <summary>
    /// Gets the raw PE section characteristics describing attributes such as executable, readable and writable permissions.
    /// </summary>
    public uint Characteristics { get; }

    /// <summary>
    /// Initializes an immutable representation of a loaded PE section.
    /// </summary>
    /// <param name="name">The section name stored in the PE section header.</param>
    /// <param name="virtualAddress">The relative virtual address of the section within the loaded image.</param>
    /// <param name="virtualSize">The section size when loaded into virtual memory.</param>
    /// <param name="rawDataSize">The section data size stored in the original PE file.</param>
    /// <param name="characteristics">The raw PE section characteristics.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    public PeSection(string name, uint virtualAddress, uint virtualSize, uint rawDataSize, uint characteristics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        VirtualAddress = virtualAddress;
        VirtualSize = virtualSize;
        RawDataSize = rawDataSize;
        Characteristics = characteristics;
    }

    /// <summary>
    /// Determines whether the specified relative virtual address belongs to this section.
    /// The loaded virtual size is preferred because PE inspection in this project operates against the in-memory image rather than the original file layout.
    /// </summary>
    /// <param name="relativeVirtualAddress">The relative virtual address to test.</param>
    /// <returns><see langword="true"/> when the address belongs to this section; otherwise <see langword="false"/>.</returns>
    public bool ContainsRva(uint relativeVirtualAddress)
    {
        uint effectiveSize = VirtualSize != 0 ? VirtualSize : RawDataSize;

        if (effectiveSize == 0 || relativeVirtualAddress < VirtualAddress)
            return false;

        return relativeVirtualAddress - VirtualAddress < effectiveSize;
    }
}