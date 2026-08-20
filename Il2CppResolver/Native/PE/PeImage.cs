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
    /// Represents the offset of <c>NumberOfRvaAndSizes</c> inside a PE32+ optional header.
    /// </summary>
    private const int NumberOfRvaAndSizesOffset = 108;

    /// <summary>
    /// Represents the offset at which PE32+ data directory entries begin inside the optional header.
    /// The export table is the first data directory entry.
    /// </summary>
    private const int DataDirectoryOffset = 112;

    /// <summary>
    /// Represents the fixed size in bytes of a PE data directory entry.
    /// Each entry contains a 32-bit RVA followed by a 32-bit size.
    /// </summary>
    private const int DataDirectorySize = 8;

    /// <summary>
    /// Represents the fixed size in bytes of an <c>IMAGE_EXPORT_DIRECTORY</c> structure.
    /// </summary>
    private const int ExportDirectorySize = 40;

    /// <summary>
    /// Represents the size in bytes of one entry in the PE export address table.
    /// </summary>
    private const int ExportAddressEntrySize = 4;

    /// <summary>
    /// Represents the size in bytes of one entry in the PE export name pointer table.
    /// </summary>
    private const int ExportNamePointerEntrySize = 4;

    /// <summary>
    /// Represents the size in bytes of one entry in the PE export ordinal table.
    /// </summary>
    private const int ExportOrdinalEntrySize = 2;

    /// <summary>
    /// Defines the maximum number of bytes accepted while reading a null-terminated PE export string.
    /// This prevents malformed images from causing unbounded remote-memory reads.
    /// </summary>
    private const int MaximumExportStringLength = 4096;

    /// <summary>
    /// Defines the number of bytes read from remote memory at once while decoding null-terminated PE export strings.
    /// </summary>
    private const int ExportStringReadChunkSize = 256;

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
    /// Gets the immutable collection of symbols exposed through the PE export table.
    /// The collection contains both named and ordinal-only exports, including forwarded exports.
    /// </summary>
    public IReadOnlyList<PeExport> Exports { get; }

    /// <summary>
    /// Initializes a validated PE image representation from already parsed header information.
    /// Instances are created exclusively through <see cref="Read(ProcessMemory, ProcessModuleInfo)"/> so callers cannot bypass PE validation.
    /// </summary>
    /// <param name="module">The native module represented by this PE image.</param>
    /// <param name="machine">The machine identifier declared by the COFF header.</param>
    /// <param name="imageSize">The loaded image size declared by the PE optional header.</param>
    /// <param name="sections">The parsed section table associated with the PE image.</param>
    /// <param name="exports">The parsed export table associated with the PE image.</param>
    private PeImage(ProcessModuleInfo module, ushort machine, uint imageSize, IReadOnlyList<PeSection> sections, IReadOnlyList<PeExport> exports)
    {
        _module = module;
        Machine = machine;
        ImageSize = imageSize;
        Sections = sections;
        Exports = exports;
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

        IReadOnlyList<PeExport> exports = ReadExports(memory, module, optionalHeaderAddress, optionalHeaderSize, imageSize);

        return new PeImage(module, machine, imageSize, sections, exports);

    }

    /// <summary>
    /// Reads and resolves all exports declared by the PE export directory.
    /// The method combines the export address table, name pointer table and ordinal table into immutable <see cref="PeExport"/> instances.
    /// </summary>
    /// <param name="memory">The process memory accessor used to inspect the loaded PE export structures.</param>
    /// <param name="module">The native module owning the PE image.</param>
    /// <param name="optionalHeaderAddress">The absolute address of the PE32+ optional header.</param>
    /// <param name="optionalHeaderSize">The optional header size declared by the COFF header.</param>
    /// <param name="imageSize">The complete virtual size declared by the PE image.</param>
    /// <returns>An immutable collection containing all valid exports declared by the image.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the PE export directory or one of its referenced tables contains inconsistent or out-of-range information.
    /// </exception>
    private static IReadOnlyList<PeExport> ReadExports(ProcessMemory memory, ProcessModuleInfo module, nint optionalHeaderAddress, ushort optionalHeaderSize, uint imageSize)
    {
        if (optionalHeaderSize < NumberOfRvaAndSizesOffset + sizeof(uint))
            throw new InvalidDataException($"Module '{module.Name}' contains an incomplete PE32+ optional header.");

        uint numberOfDataDirectories = memory.Read<uint>(optionalHeaderAddress + NumberOfRvaAndSizesOffset);

        if (numberOfDataDirectories == 0)
            return Array.Empty<PeExport>();

        if (optionalHeaderSize < DataDirectoryOffset + DataDirectorySize)
            throw new InvalidDataException($"Module '{module.Name}' declares PE data directories but does not contain a complete export directory entry.");

        uint exportDirectoryRva = memory.Read<uint>(optionalHeaderAddress + DataDirectoryOffset);
        uint exportDirectorySize = memory.Read<uint>(optionalHeaderAddress + DataDirectoryOffset + sizeof(uint));

        if (exportDirectoryRva == 0 && exportDirectorySize == 0)
            return Array.Empty<PeExport>();

        if (exportDirectoryRva == 0 || exportDirectorySize < ExportDirectorySize)
            throw new InvalidDataException($"Module '{module.Name}' contains an invalid PE export directory.");

        EnsureRvaRange(module, imageSize, exportDirectoryRva, exportDirectorySize, "export directory");

        byte[] exportDirectory = memory.ReadBytes(module.BaseAddress + (nint)exportDirectoryRva, ExportDirectorySize);

        uint ordinalBase = BitConverter.ToUInt32(exportDirectory, 16);
        uint numberOfFunctions = BitConverter.ToUInt32(exportDirectory, 20);
        uint numberOfNames = BitConverter.ToUInt32(exportDirectory, 24);
        uint addressOfFunctions = BitConverter.ToUInt32(exportDirectory, 28);
        uint addressOfNames = BitConverter.ToUInt32(exportDirectory, 32);
        uint addressOfNameOrdinals = BitConverter.ToUInt32(exportDirectory, 36);

        if (numberOfFunctions == 0)
            return Array.Empty<PeExport>();

        if (numberOfNames > numberOfFunctions)
            throw new InvalidDataException($"Module '{module.Name}' declares more named exports than export address entries.");

        EnsureTableRange(module, imageSize, addressOfFunctions, numberOfFunctions, ExportAddressEntrySize, "export address table");

        if (numberOfNames > 0)
        {
            EnsureTableRange(module, imageSize, addressOfNames, numberOfNames, ExportNamePointerEntrySize, "export name pointer table");
            EnsureTableRange(module, imageSize, addressOfNameOrdinals, numberOfNames, ExportOrdinalEntrySize, "export ordinal table");
        }

        byte[] functionTable = memory.ReadBytes(module.BaseAddress + (nint)addressOfFunctions, checked((int)(numberOfFunctions * ExportAddressEntrySize)));
        byte[] nameTable = numberOfNames > 0
            ? memory.ReadBytes(module.BaseAddress + (nint)addressOfNames, checked((int)(numberOfNames * ExportNamePointerEntrySize)))
            : Array.Empty<byte>();
        byte[] ordinalTable = numberOfNames > 0
            ? memory.ReadBytes(module.BaseAddress + (nint)addressOfNameOrdinals, checked((int)(numberOfNames * ExportOrdinalEntrySize)))
            : Array.Empty<byte>();

        List<PeExport> exports = new();
        HashSet<uint> namedFunctionIndexes = new();

        for (uint nameIndex = 0; nameIndex < numberOfNames; nameIndex++)
        {
            int namePointerOffset = checked((int)(nameIndex * ExportNamePointerEntrySize));
            int ordinalOffset = checked((int)(nameIndex * ExportOrdinalEntrySize));

            uint nameRva = BitConverter.ToUInt32(nameTable, namePointerOffset);
            ushort functionIndex = BitConverter.ToUInt16(ordinalTable, ordinalOffset);

            if (functionIndex >= numberOfFunctions)
                throw new InvalidDataException($"Module '{module.Name}' contains an export ordinal index outside the export address table.");

            string name = ReadAsciiString(memory, module, imageSize, nameRva);
            uint functionRva = ReadFunctionRva(functionTable, functionIndex);

            if (functionRva == 0)
                throw new InvalidDataException($"Named export '{name}' in module '{module.Name}' references an empty export address entry.");

            uint ordinal = GetPublicOrdinal(module, ordinalBase, functionIndex);

            exports.Add(CreateExport(memory, module, imageSize, exportDirectoryRva, exportDirectorySize, name, ordinal, functionRva));
            namedFunctionIndexes.Add(functionIndex);
        }

        for (uint functionIndex = 0; functionIndex < numberOfFunctions; functionIndex++)
        {
            if (namedFunctionIndexes.Contains(functionIndex))
                continue;

            uint functionRva = ReadFunctionRva(functionTable, functionIndex);

            // Zero-valued entries represent unused slots in the export address table rather than actual exports.
            if (functionRva == 0)
                continue;

            uint ordinal = GetPublicOrdinal(module, ordinalBase, functionIndex);

            exports.Add(CreateExport(memory, module, imageSize, exportDirectoryRva, exportDirectorySize, null, ordinal, functionRva));
        }

        return exports.AsReadOnly();
    }

    /// <summary>
    /// Creates a semantic PE export from an export address table entry.
    /// An RVA located inside the export directory range is interpreted as a forwarder string according to the PE specification; all other RVAs are treated as direct image addresses.
    /// </summary>
    /// <param name="memory">The process memory accessor used when a forwarder string must be decoded.</param>
    /// <param name="module">The module owning the export.</param>
    /// <param name="imageSize">The complete virtual size declared by the PE image.</param>
    /// <param name="exportDirectoryRva">The RVA at which the PE export directory begins.</param>
    /// <param name="exportDirectorySize">The complete size of the PE export directory range.</param>
    /// <param name="name">The public export name, or <see langword="null"/> for an ordinal-only export.</param>
    /// <param name="ordinal">The public ordinal assigned to the export.</param>
    /// <param name="functionRva">The RVA stored in the export address table.</param>
    /// <returns>A validated direct or forwarded PE export.</returns>
    private static PeExport CreateExport(ProcessMemory memory, ProcessModuleInfo module, uint imageSize, uint exportDirectoryRva, uint exportDirectorySize, string? name, uint ordinal, uint functionRva)
    {
        bool isForwarded = IsRvaInRange(functionRva, exportDirectoryRva, exportDirectorySize);

        if (isForwarded)
        {
            string forwarderName = ReadAsciiString(memory, module, imageSize, functionRva);
            return new PeExport(name, ordinal, functionRva, null, forwarderName);
        }

        EnsureRvaRange(module, imageSize, functionRva, 1, $"export '{name ?? $"#{ordinal}"}'");

        nint address = module.BaseAddress + (nint)functionRva;

        return new PeExport(name, ordinal, functionRva, address, null);
    }

    /// <summary>
    /// Reads a function RVA from an already loaded PE export address table.
    /// </summary>
    /// <param name="functionTable">The complete export address table.</param>
    /// <param name="functionIndex">The unbiased ordinal index identifying the requested address table entry.</param>
    /// <returns>The relative virtual address stored in the requested export address entry.</returns>
    private static uint ReadFunctionRva(byte[] functionTable, uint functionIndex)
    {
        int functionOffset = checked((int)(functionIndex * ExportAddressEntrySize));
        return BitConverter.ToUInt32(functionTable, functionOffset);
    }

    /// <summary>
    /// Determines whether a relative virtual address belongs to the specified RVA range.
    /// </summary>
    /// <param name="relativeVirtualAddress">The RVA to test.</param>
    /// <param name="rangeStart">The RVA at which the range begins.</param>
    /// <param name="rangeSize">The number of bytes occupied by the range.</param>
    /// <returns><see langword="true"/> when the address belongs to the range; otherwise <see langword="false"/>.</returns>
    private static bool IsRvaInRange(uint relativeVirtualAddress, uint rangeStart, uint rangeSize)
    {
        if (rangeSize == 0 || relativeVirtualAddress < rangeStart)
            return false;

        return (ulong)relativeVirtualAddress - rangeStart < rangeSize;
    }

    /// <summary>
    /// Converts an unbiased export address table index into the public ordinal declared by the PE image.
    /// </summary>
    /// <param name="module">The module owning the export table.</param>
    /// <param name="ordinalBase">The ordinal base declared by the export directory.</param>
    /// <param name="functionIndex">The unbiased index inside the export address table.</param>
    /// <returns>The public ordinal assigned to the export.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the resulting ordinal exceeds the range of an unsigned 32-bit value.
    /// </exception>
    private static uint GetPublicOrdinal(ProcessModuleInfo module, uint ordinalBase, uint functionIndex)
    {
        ulong ordinal = (ulong)ordinalBase + functionIndex;

        if (ordinal > uint.MaxValue)
            throw new InvalidDataException($"Module '{module.Name}' contains an export ordinal that exceeds the supported range.");

        return (uint)ordinal;
    }

    /// <summary>
    /// Validates that a PE table described by a relative virtual address, entry count and entry size remains entirely inside the loaded image.
    /// </summary>
    /// <param name="module">The module owning the table.</param>
    /// <param name="imageSize">The complete virtual size declared by the PE image.</param>
    /// <param name="relativeVirtualAddress">The RVA at which the table begins.</param>
    /// <param name="entryCount">The number of entries declared by the table.</param>
    /// <param name="entrySize">The fixed size in bytes of each table entry.</param>
    /// <param name="description">A diagnostic description of the table being validated.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the table is empty, overflows its calculated size or falls outside the loaded image.
    /// </exception>
    private static void EnsureTableRange(ProcessModuleInfo module, uint imageSize, uint relativeVirtualAddress, uint entryCount, uint entrySize, string description)
    {
        if (relativeVirtualAddress == 0 || entryCount == 0 || entrySize == 0)
            throw new InvalidDataException($"Module '{module.Name}' contains an invalid {description}.");

        ulong size = (ulong)entryCount * entrySize;

        EnsureRvaRange(module, imageSize, relativeVirtualAddress, size, description);

        if (size > int.MaxValue)
            throw new InvalidDataException($"Module '{module.Name}' contains a {description} that exceeds the supported managed buffer size.");
    }

    /// <summary>
    /// Validates that a relative virtual address range remains entirely inside the loaded PE image.
    /// </summary>
    /// <param name="module">The module owning the PE image.</param>
    /// <param name="imageSize">The complete virtual size declared by the PE image.</param>
    /// <param name="relativeVirtualAddress">The RVA at which the range begins.</param>
    /// <param name="size">The number of bytes occupied by the range.</param>
    /// <param name="description">A diagnostic description of the range being validated.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the range starts outside the image or extends beyond its declared virtual size.
    /// </exception>
    private static void EnsureRvaRange(ProcessModuleInfo module, uint imageSize, uint relativeVirtualAddress, ulong size, string description)
    {
        ulong start = relativeVirtualAddress;
        ulong end = start + size;

        if (size == 0 || start >= imageSize || end > imageSize || end < start)
            throw new InvalidDataException($"Module '{module.Name}' contains a {description} outside the declared PE image range.");
    }

    /// <summary>
    /// Reads a null-terminated ASCII string from an RVA inside the loaded PE image.
    /// Remote memory is read in bounded chunks and the operation fails if no terminator is found before the configured safety limit.
    /// </summary>
    /// <param name="memory">The process memory accessor used to read the remote string.</param>
    /// <param name="module">The module containing the string.</param>
    /// <param name="imageSize">The complete virtual size declared by the PE image.</param>
    /// <param name="relativeVirtualAddress">The RVA at which the ASCII string begins.</param>
    /// <returns>The decoded ASCII string without its null terminator.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the string starts outside the image, is empty or does not terminate within the configured maximum length.
    /// </exception>
    private static string ReadAsciiString(ProcessMemory memory, ProcessModuleInfo module, uint imageSize, uint relativeVirtualAddress)
    {
        EnsureRvaRange(module, imageSize, relativeVirtualAddress, 1, "export string");

        List<byte> bytes = new();
        uint currentRva = relativeVirtualAddress;

        while (bytes.Count < MaximumExportStringLength)
        {
            uint bytesRemainingInImage = imageSize - currentRva;
            int bytesRemainingInLimit = MaximumExportStringLength - bytes.Count;
            int chunkSize = (int)Math.Min((uint)Math.Min(ExportStringReadChunkSize, bytesRemainingInLimit), bytesRemainingInImage);

            if (chunkSize <= 0)
                break;

            byte[] chunk = memory.ReadBytes(module.BaseAddress + (nint)currentRva, chunkSize);
            int terminatorIndex = Array.IndexOf(chunk, (byte)0);

            if (terminatorIndex >= 0)
            {
                bytes.AddRange(chunk.AsSpan(0, terminatorIndex).ToArray());

                if (bytes.Count == 0)
                    throw new InvalidDataException($"Module '{module.Name}' contains an empty PE export string at RVA 0x{relativeVirtualAddress:X8}.");

                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            bytes.AddRange(chunk);
            currentRva += (uint)chunkSize;
        }

        throw new InvalidDataException($"Module '{module.Name}' contains an unterminated PE export string at RVA 0x{relativeVirtualAddress:X8}.");
    }

    /// <summary>
    /// Searches the PE export table for a symbol matching the specified public name.
    /// Export names are compared using ordinal case-sensitive semantics, matching the native PE symbol naming model.
    /// </summary>
    /// <param name="name">The public export name to locate.</param>
    /// <returns>The matching export when found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    public PeExport? FindExport(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (PeExport export in Exports)
        {
            if (string.Equals(export.Name, name, StringComparison.Ordinal))
                return export;
        }

        return null;
    }

    /// <summary>
    /// Retrieves a required PE export by its public name.
    /// This method is intended for runtime discovery components that cannot continue when a specific native entry point is unavailable.
    /// </summary>
    /// <param name="name">The public export name to retrieve.</param>
    /// <returns>The matching exported symbol.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested export does not exist in the PE image.
    /// </exception>
    public PeExport GetRequiredExport(string name)
    {
        PeExport? export = FindExport(name);

        if (export is null)
            throw new InvalidOperationException($"Export '{name}' was not found in module '{_module.Name}'.");

        return export;
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