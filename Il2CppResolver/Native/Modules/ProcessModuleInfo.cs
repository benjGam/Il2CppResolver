namespace UnityIl2CppResolver.Native.Modules;

/// <summary>
/// Describes a native module currently loaded in the virtual address space of the target process.
/// This immutable model belongs to the native discovery layer and provides higher-level components with the module boundaries and filesystem identity required for PE inspection.
/// It contains no Unity or IL2CPP-specific knowledge; modules such as GameAssembly.dll are interpreted only by higher architectural layers.
/// </summary>
internal sealed record ProcessModuleInfo
{
    /// <summary>
    /// Gets the file name of the loaded module, such as <c>GameAssembly.dll</c> or <c>UnityPlayer.dll</c>.
    /// This value is used by module discovery consumers to identify a module without depending on its installation path.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the absolute filesystem path of the loaded module as reported by the target process.
    /// The path can later be used for file-based PE inspection or diagnostics when required.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the virtual base address at which the module is loaded in the target process.
    /// All RVAs belonging to this module are interpreted relative to this address.
    /// </summary>
    public nint BaseAddress { get; }

    /// <summary>
    /// Gets the total mapped image size of the module in the target process.
    /// Together with <see cref="BaseAddress"/>, this defines the virtual address range owned by the loaded image.
    /// </summary>
    public nuint Size { get; }

    /// <summary>
    /// Initializes an immutable description of a loaded process module.
    /// </summary>
    /// <param name="name">The file name identifying the loaded module.</param>
    /// <param name="path">The absolute filesystem path of the loaded module.</param>
    /// <param name="baseAddress">The virtual base address at which the module is loaded.</param>
    /// <param name="size">The total mapped image size of the module.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="path"/> is empty or contains only whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="baseAddress"/> is zero or when <paramref name="size"/> is zero.
    /// </exception>
    public ProcessModuleInfo(string name, string path, nint baseAddress, nuint size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (baseAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(baseAddress), "The module base address cannot be zero.");

        if (size == 0)
            throw new ArgumentOutOfRangeException(nameof(size), "The module image size cannot be zero.");

        Name = name;
        Path = path;
        BaseAddress = baseAddress;
        Size = size;
    }

    /// <summary>
    /// Determines whether the specified virtual address belongs to the memory range occupied by this module.
    /// This helper provides a generic range check that can later be reused by PE and resolution validation components.
    /// </summary>
    /// <param name="address">The virtual address to test against the module range.</param>
    /// <returns><see langword="true"/> when the address belongs to this module; otherwise <see langword="false"/>.</returns>
    public bool ContainsAddress(nint address)
    {
        if (address == 0)
            return false;

        nuint relativeAddress = unchecked((nuint)address - (nuint)BaseAddress);
        return relativeAddress < Size;
    }
}