using System.Text;
using UnityIl2CppResolver.Il2Cpp.Runtime;
using UnityIl2CppResolver.Native.Memory;

namespace UnityIl2CppResolver.Il2Cpp.Values;

/// <summary>
/// Decodes live IL2CPP <c>System.String</c> instances through public runtime string APIs and read-only target memory.
/// The reader applies a defensive character-count limit and validates the complete UTF-16 payload range before allocating or decoding local managed text.
/// </summary>
internal sealed class Il2CppStringReader
{
    /// <summary>Defines the maximum UTF-16 character count accepted from one remote managed string.</summary>
    private const int MaximumCharacterCount = 1_048_576;
    /// <summary>Provides safe read-only access to the target process address space.</summary>
    private readonly ProcessMemory _memory;
    /// <summary>Provides public IL2CPP string APIs used to retrieve length and character-buffer identity.</summary>
    private readonly Il2CppRuntime _runtime;
    /// <summary>Defines the timeout applied to individual runtime calls.</summary>
    private readonly TimeSpan _callTimeout;

    /// <summary>Initializes a managed-string reader for one resolver session.</summary>
    /// <param name="memory">The read-only target process memory accessor.</param>
    /// <param name="runtime">The live IL2CPP runtime facade.</param>
    /// <param name="callTimeout">The timeout applied to individual runtime calls.</param>
    public Il2CppStringReader(ProcessMemory memory, Il2CppRuntime runtime, TimeSpan callTimeout)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(runtime);

        if (callTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(callTimeout), "The runtime call timeout must be positive.");

        _memory = memory;
        _runtime = runtime;
        _callTimeout = callTimeout;
    }

    /// <summary>Decodes one remote managed string while preserving the distinction between null and empty strings.</summary>
    /// <param name="stringAddress">The remote <c>Il2CppString*</c> address, or zero for a null managed reference.</param>
    /// <returns>The decoded managed string, <see cref="string.Empty"/> for a zero-length string, or <see langword="null"/> for a null reference.</returns>
    public string? Read(nint stringAddress)
    {
        if (stringAddress == 0)
            return null;

        int length = _runtime.GetStringLength(stringAddress, _callTimeout);

        if (length > MaximumCharacterCount)
            throw new InvalidDataException($"IL2CPP string at 0x{stringAddress:X} contains {length} UTF-16 character(s), exceeding the defensive limit of {MaximumCharacterCount}.");

        if (length == 0)
            return string.Empty;

        nint charsAddress = _runtime.GetStringChars(stringAddress, _callTimeout);
        int byteLength = checked(length * sizeof(char));

        if (!_memory.IsReadableRange(charsAddress, checked((nuint)byteLength)))
            throw new InvalidDataException($"IL2CPP string character range at 0x{charsAddress:X} with size 0x{byteLength:X} is not completely readable.");

        byte[] bytes = _memory.ReadBytes(charsAddress, byteLength);
        return Encoding.Unicode.GetString(bytes);
    }
}
