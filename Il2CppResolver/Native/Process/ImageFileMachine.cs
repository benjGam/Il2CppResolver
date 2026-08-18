namespace UnityIl2CppResolver.Native.Process;

/// <summary>
/// Identifies Windows PE machine architectures as exposed by process architecture inspection APIs.
/// The resolver currently uses these values to enforce its Windows x64 execution scope during target attachment.
/// </summary>
internal enum ImageFileMachine : ushort
{
    /// <summary>
    /// Indicates that the process uses the native architecture of the operating system.
    /// </summary>
    Unknown = 0x0000,

    /// <summary>
    /// Identifies the 32-bit x86 architecture.
    /// </summary>
    I386 = 0x014C,

    /// <summary>
    /// Identifies the 64-bit x86 architecture supported by the current resolver implementation.
    /// </summary>
    Amd64 = 0x8664,

    /// <summary>
    /// Identifies the 64-bit ARM architecture.
    /// </summary>
    Arm64 = 0xAA64
}