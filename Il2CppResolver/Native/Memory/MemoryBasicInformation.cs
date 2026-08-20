using System.Runtime.InteropServices;

namespace UnityIl2CppResolver.Native.Memory;

/// <summary>
/// Mirrors the Windows <c>MEMORY_BASIC_INFORMATION</c> structure returned by <c>VirtualQueryEx</c>.
/// This structure belongs to the generic native memory layer and describes one contiguous virtual-memory region sharing the same allocation state and page protection.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MemoryBasicInformation
{
    /// <summary>
    /// Represents the base virtual address of the memory region described by this structure.
    /// </summary>
    public nint BaseAddress;

    /// <summary>
    /// Represents the base address of the original allocation containing this region.
    /// </summary>
    public nint AllocationBase;

    /// <summary>
    /// Represents the page protection originally assigned when the allocation was created.
    /// </summary>
    public uint AllocationProtect;

    /// <summary>
    /// Represents the Windows memory partition identifier associated with the region.
    /// </summary>
    public ushort PartitionId;

    /// <summary>
    /// Represents the size in bytes of the contiguous region beginning at <see cref="BaseAddress"/>.
    /// </summary>
    public nuint RegionSize;

    /// <summary>
    /// Represents the current allocation state of the pages contained by the region.
    /// </summary>
    public uint State;

    /// <summary>
    /// Represents the current page protection applied to the region.
    /// </summary>
    public uint Protect;

    /// <summary>
    /// Represents the native Windows memory type associated with the region.
    /// </summary>
    public uint Type;
}