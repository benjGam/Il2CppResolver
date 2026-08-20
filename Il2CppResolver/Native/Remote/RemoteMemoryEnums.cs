namespace UnityIl2CppResolver.Native.Remote;

/// <summary>
/// Defines the allocation modes supported by the remote execution layer when reserving memory inside the target process.
/// These values map directly to the corresponding Windows virtual-memory allocation flags.
/// </summary>
[Flags]
internal enum MemoryAllocationType : uint
{
    /// <summary>
    /// Commits physical storage for the requested virtual-memory pages.
    /// </summary>
    Commit = 0x00001000,

    /// <summary>
    /// Reserves a range of virtual address space without initially mapping physical storage.
    /// </summary>
    Reserve = 0x00002000
}

/// <summary>
/// Defines the memory protection modes required by short-lived remote call allocations.
/// The remote execution layer initially writes generated code into read-write memory and only grants execute permission after the code has been fully initialized.
/// </summary>
internal enum MemoryProtection : uint
{
    /// <summary>
    /// Allows both reading and writing while remote data or generated code is being initialized.
    /// </summary>
    ReadWrite = 0x00000004,

    /// <summary>
    /// Allows reading and execution after a generated trampoline has been fully initialized.
    /// </summary>
    ExecuteRead = 0x00000020
}

/// <summary>
/// Defines the release operation used when returning a complete remote allocation to the target process virtual address space.
/// </summary>
internal enum MemoryFreeType : uint
{
    /// <summary>
    /// Releases the complete region previously reserved by <c>VirtualAllocEx</c>.
    /// </summary>
    Release = 0x00008000
}