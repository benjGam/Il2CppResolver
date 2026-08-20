using System.Text;
using UnityIl2CppResolver.Native.Memory;
using UnityIl2CppResolver.Native.Modules;

namespace UnityIl2CppResolver.Native.PE;

/// <summary>
/// Represents a validated 64-bit Portable Executable image loaded in the virtual address space of the target process.
/// This component belongs to the native inspection layer and parses PE headers directly from remote memory through <see cref="ProcessMemory"/>.
/// Higher-level Unity and IL2CPP discovery components rely on this class to reason about executable image boundaries, sections and eventually exported symbols without reading the module file from disk.
/// </summary>
public sealed class PeImage
{
    /// <summary>
    /// Represents the expected DOS header signature identifying the beginning of a valid PE image.
    /// </summary>
    private const ushort DosSignature = 0x5A4D;

    /// <summary>
    /// Represents the expected four-byte PE signature located at the offset referenced by the DOS header.
    /// </summary>
    private const uint PeSignature = 0x00004550;

    /// <summary>
    /// Represents the PE optional header magic identifying a 64-bit PE32+ image.
    /// </summary>
    private const ushort Pe32PlusMagic = 0x020B;

    /// <summary>
    /// Represents the native AMD64 machine identifier expected by the current resolver architecture.
    /// </summary>
    private const ushort Amd64Machine = 0x8664;

    /// <summary>
    /// Represents the location of the PE header offset field inside the DOS header.
    /// </summary>
    private const int PeHeaderOffsetLocation = 0x3C;

    /// <summary>
    /// Represents the fixed size in bytes of the PE signature located before the COFF file header.
    /// </summary>
    private const int PeSignatureSize = 4;

    /// <summary>
    /// Represents the fixed size in bytes of the standard COFF file header.
    /// </summary>
    private const int CoffHeaderSize = 20;

    /// <summary>
    /// Represents the fixed size in bytes of one PE section header entry.
    /// </summary>
    private const int SectionHeaderSize = 40;

    /// <summary>
    /// Represents the offset of <c>SizeOfImage</c> inside a PE32+ optional header.
    /// </summary>
    private const int SizeOfImageOffset = 56;

    /// <summary>
    /// Represents the process memory accessor used to read PE headers from the loaded image.
    /// </summary>
    private readonly ProcessMemory _memory;

    /// <summary>
    /// Represents the native module whose loaded image is described by this instance.
    /// </summary>
    private readonly ProcessModuleInfo _module;

    /// <summary>
    /// Gets the virtual base address at which the PE image is loaded in the target process.
    /// </summary>
    public nint BaseAddress => _module.BaseAddress;

    /// <summary>
    /// Gets the image size declared by the PE optional header.
    /// This value represents the complete memory footprint reserved for the loaded image according to the PE metadata.
    /// </summary>
    public uint ImageSize { get; }

    /// <summary>
    /// Gets the machine identifier declared by the PE COFF header.
    /// The current resolver accepts only native AMD64 images.
    /// </summary>
    public ushort Machine { get; }

    /// <summary>
    /// Gets the immutable collection of sections declared by the loaded PE image.
    /// </summary>
    public IReadOnlyList<PeSection> Sections { get; }

    /// <summary>
    /// Initializes a validated PE image representation from already parsed header information.
    /// Instances are created exclusively through <see cref="Read(ProcessMemory, ProcessModuleInfo)"/> so callers cannot bypass PE validation.
    /// </summary>
    /// <param name="memory">The process memory accessor associated with the target process.</param>
    /// <param name="module">The native module represented by this PE image.</param>
    /// <param name="machine">The machine identifier declared by the COFF header.</param>
    /// <param name="imageSize">The loaded image size declared by the PE optional header.</param>
    /// <param name="sections">The parsed section table associated with the PE image.</param>
    private PeImage(ProcessMemory memory, ProcessModuleInfo module, ushort machine, uint imageSize, IReadOnlyList<PeSection> sections)
    {
        _memory = memory;
        _module = module;
        Machine = machine;
        ImageSize = imageSize;
        Sections = sections;
    }

    /// <summary>
    /// Reads and validates a 64-bit PE image directly from the memory of the target process.
    /// The method validates the DOS signature, PE signature, AMD64 machine type and PE32+ optional header before parsing the section table.
    /// </summary>
    /// <param name="memory">The process memory accessor used to inspect the loaded image.</param>
    /// <param name="module">The loaded native module whose base address identifies the beginning of the PE image.</param>
    /// <returns>A validated <see cref="PeImage"/> describing the loaded module.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="memory"/> or <paramref name="module"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the loaded module does not contain a valid supported AMD64 PE32+ image.
    /// </exception>
    public static PeImage Read(ProcessMemory memory, ProcessModuleInfo module)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(module);

        ushort dosSignature = memory.Read<ushort>(module.BaseAddress);

        if (dosSignature != DosSignature)
            throw new InvalidDataException($"Module '{module.Name}' does not contain a valid DOS header.");

        int peHeaderOffset = memory.Read<int>(module.BaseAddress + PeHeaderOffsetLocation);

        if (peHeaderOffset <= 0 || (nuint)peHeaderOffset >= module.Size)
            throw new InvalidDataException($"Module '{module.Name}' contains an invalid PE header offset.");

        nint peHeaderAddress = module.BaseAddress + peHeaderOffset;
        uint peSignature = memory.Read<uint>(peHeaderAddress);

        if (peSignature != PeSignature)
            throw new InvalidDataException($"Module '{module.Name}' does not contain a valid PE signature.");

        nint coffHeaderAddress = peHeaderAddress + PeSignatureSize;

        ushort machine = memory.Read<ushort>(coffHeaderAddress);
        ushort numberOfSections = memory.Read<ushort>(coffHeaderAddress + 2);
        ushort optionalHeaderSize = memory.Read<ushort>(coffHeaderAddress + 16);

        if (machine != Amd64Machine)
            throw new InvalidDataException($"Module '{module.Name}' is not an AMD64 PE image.");

        if (numberOfSections is 0 or > 96)
            throw new InvalidDataException($"Module '{module.Name}' declares an invalid number of PE sections: {numberOfSections}.");

        if (optionalHeaderSize == 0)
            throw new InvalidDataException($"Module '{module.Name}' does not contain a PE optional header.");

        nint optionalHeaderAddress = coffHeaderAddress + CoffHeaderSize;
        ushort optionalHeaderMagic = memory.Read<ushort>(optionalHeaderAddress);

        if (optionalHeaderMagic != Pe32PlusMagic)
            throw new InvalidDataException($"Module '{module.Name}' is not a PE32+ image.");

        if (optionalHeaderSize < SizeOfImageOffset + sizeof(uint))
            throw new InvalidDataException($"Module '{module.Name}' contains an incomplete PE32+ optional header.");

        uint imageSize = memory.Read<uint>(optionalHeaderAddress + SizeOfImageOffset);

        if (imageSize == 0)
            throw new InvalidDataException($"Module '{module.Name}' declares an invalid image size.");

        IReadOnlyList<PeSection> sections = ReadSections(memory, module, optionalHeaderAddress, optionalHeaderSize, numberOfSections);

        return new PeImage(memory, module, machine, imageSize, sections);
    }

    /// <summary>
    /// Searches the parsed PE section table for a section matching the specified name.
    /// Section names are matched using ordinal semantics because PE section identifiers are byte-level image metadata rather than culture-sensitive strings.
    /// </summary>
    /// <param name="name">The PE section name to locate.</param>
    /// <returns>The matching section when found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    public PeSection? FindSection(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (PeSection section in Sections)
        {
            if (string.Equals(section.Name, name, StringComparison.Ordinal))
                return section;
        }

        return null;
    }

    /// <summary>
    /// Retrieves a required PE section by name.
    /// This method is intended for higher-level components that cannot continue when a specific section is absent from the loaded image.
    /// </summary>
    /// <param name="name">The PE section name to retrieve.</param>
    /// <returns>The matching parsed section.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested section does not exist in the image.
    /// </exception>
    public PeSection GetRequiredSection(string name)
    {
        PeSection? section = FindSection(name);

        if (section is null)
            throw new InvalidOperationException($"Section '{name}' was not found in module '{_module.Name}'.");

        return section;
    }

    /// <summary>
    /// Converts a relative virtual address belonging to this image into its absolute runtime address.
    /// </summary>
    /// <param name="relativeVirtualAddress">The relative virtual address to convert.</param>
    /// <returns>The absolute virtual address inside the target process.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the relative virtual address falls outside the declared PE image range.
    /// </exception>
    public nint RvaToAddress(uint relativeVirtualAddress)
    {
        if (relativeVirtualAddress >= ImageSize)
            throw new ArgumentOutOfRangeException(nameof(relativeVirtualAddress), $"RVA 0x{relativeVirtualAddress:X} falls outside module '{_module.Name}'.");

        return BaseAddress + (nint)relativeVirtualAddress;
    }

    /// <summary>
    /// Determines whether the specified absolute virtual address belongs to this loaded PE image.
    /// </summary>
    /// <param name="address">The absolute virtual address to test.</param>
    /// <returns><see langword="true"/> when the address belongs to the loaded image; otherwise <see langword="false"/>.</returns>
    public bool ContainsAddress(nint address)
    {
        return _module.ContainsAddress(address);
    }

    /// <summary>
    /// Reads all section entries declared immediately after the PE optional header.
    /// Each native section header is converted into the immutable <see cref="PeSection"/> representation used by the rest of the native layer.
    /// </summary>
    /// <param name="memory">The process memory accessor used to read remote section headers.</param>
    /// <param name="module">The module owning the PE image.</param>
    /// <param name="optionalHeaderAddress">The absolute address of the PE optional header.</param>
    /// <param name="optionalHeaderSize">The exact optional header size declared by the COFF header.</param>
    /// <param name="numberOfSections">The number of section entries declared by the COFF header.</param>
    /// <returns>An immutable collection containing all parsed PE sections.</returns>
    private static IReadOnlyList<PeSection> ReadSections(ProcessMemory memory, ProcessModuleInfo module, nint optionalHeaderAddress, ushort optionalHeaderSize, ushort numberOfSections)
    {
        nint sectionTableAddress = optionalHeaderAddress + optionalHeaderSize;
        List<PeSection> sections = new(numberOfSections);

        for (int index = 0; index < numberOfSections; index++)
        {
            nint sectionHeaderAddress = sectionTableAddress + index * SectionHeaderSize;
            byte[] sectionHeader = memory.ReadBytes(sectionHeaderAddress, SectionHeaderSize);

            string name = ReadSectionName(sectionHeader);
            uint virtualSize = BitConverter.ToUInt32(sectionHeader, 8);
            uint virtualAddress = BitConverter.ToUInt32(sectionHeader, 12);
            uint rawDataSize = BitConverter.ToUInt32(sectionHeader, 16);
            uint characteristics = BitConverter.ToUInt32(sectionHeader, 36);

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException($"Module '{module.Name}' contains an unnamed PE section at index {index}.");

            sections.Add(new PeSection(name, virtualAddress, virtualSize, rawDataSize, characteristics));
        }

        return sections.AsReadOnly();
    }

    /// <summary>
    /// Decodes the fixed eight-byte name field stored at the beginning of a native PE section header.
    /// Section names shorter than eight bytes are terminated by a null byte.
    /// </summary>
    /// <param name="sectionHeader">The complete forty-byte native PE section header.</param>
    /// <returns>The decoded PE section name.</returns>
    private static string ReadSectionName(ReadOnlySpan<byte> sectionHeader)
    {
        ReadOnlySpan<byte> nameBytes = sectionHeader[..8];
        int nullIndex = nameBytes.IndexOf((byte)0);

        if (nullIndex >= 0)
            nameBytes = nameBytes[..nullIndex];

        return Encoding.ASCII.GetString(nameBytes);
    }
}