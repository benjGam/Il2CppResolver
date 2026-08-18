namespace UnityIl2CppResolver.Native.Process;

/// <summary>
/// Defines the native Windows access rights that can be requested when opening a target process.
/// The resolver deliberately uses the smallest subset required by each architectural layer instead of requesting unrestricted process access.
/// </summary>
[Flags]
internal enum ProcessAccessRights : uint
{
    /// <summary>
    /// Allows the caller to read memory from the target process through native virtual-memory APIs.
    /// </summary>
    VirtualMemoryRead = 0x0010,

    /// <summary>
    /// Allows limited process information queries such as architecture and lifecycle inspection.
    /// </summary>
    QueryLimitedInformation = 0x1000
}