namespace UnityIl2CppResolver.Native.PE;

/// <summary>
/// Describes a symbol exposed through the export table of a loaded Portable Executable image.
/// This immutable model belongs to the native PE inspection layer and represents either a direct export backed by an address inside the image or a forwarded export referencing another module.
/// Higher-level runtime discovery components can use exported symbols without parsing raw PE export structures or making assumptions about their underlying representation.
/// </summary>
internal sealed record PeExport
{
    /// <summary>
    /// Gets the public name associated with the exported symbol.
    /// This value is <see langword="null"/> when the symbol is exported exclusively by ordinal.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the public ordinal assigned to the exported symbol.
    /// This value includes the ordinal base declared by the PE export directory.
    /// </summary>
    public uint Ordinal { get; }

    /// <summary>
    /// Gets the relative virtual address stored in the PE export address table.
    /// For direct exports, this RVA identifies code or data inside the loaded image.
    /// For forwarded exports, this RVA identifies the forwarder string stored inside the export directory range.
    /// </summary>
    public uint RelativeVirtualAddress { get; }

    /// <summary>
    /// Gets the absolute runtime address of a direct exported symbol.
    /// This value is <see langword="null"/> for forwarded exports because their export table entry identifies a forwarder string rather than executable code or data.
    /// </summary>
    public nint? Address { get; }

    /// <summary>
    /// Gets a value indicating whether this export forwards resolution to a symbol exposed by another module.
    /// </summary>
    public bool IsForwarded { get; }

    /// <summary>
    /// Gets the forwarder string associated with this export.
    /// Typical values follow the form <c>MODULE.Symbol</c> or <c>MODULE.#Ordinal</c>.
    /// This value is <see langword="null"/> for direct exports.
    /// </summary>
    public string? ForwarderName { get; }

    /// <summary>
    /// Initializes an immutable representation of a PE export and validates the relationship between direct and forwarded export information.
    /// Instances are created by the PE parser after resolving the export address table and associated name and ordinal tables.
    /// </summary>
    /// <param name="name">The public export name, or <see langword="null"/> when the symbol is exported only by ordinal.</param>
    /// <param name="ordinal">The public ordinal assigned to the exported symbol.</param>
    /// <param name="relativeVirtualAddress">The RVA stored in the export address table.</param>
    /// <param name="address">The absolute runtime address for a direct export, or <see langword="null"/> for a forwarded export.</param>
    /// <param name="forwarderName">The forwarder string for a forwarded export, or <see langword="null"/> for a direct export.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="relativeVirtualAddress"/> is zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the supplied direct or forwarded export information is inconsistent.
    /// </exception>
    internal PeExport(string? name, uint ordinal, uint relativeVirtualAddress, nint? address, string? forwarderName)
    {
        if (relativeVirtualAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(relativeVirtualAddress), "The export relative virtual address cannot be zero.");

        bool isForwarded = forwarderName is not null;

        if (isForwarded && address is not null)
            throw new ArgumentException("A forwarded export cannot expose a direct runtime address.", nameof(address));

        if (!isForwarded && address is null)
            throw new ArgumentException("A direct export must expose a runtime address.", nameof(address));

        Name = name;
        Ordinal = ordinal;
        RelativeVirtualAddress = relativeVirtualAddress;
        Address = address;
        IsForwarded = isForwarded;
        ForwarderName = forwarderName;
    }
}