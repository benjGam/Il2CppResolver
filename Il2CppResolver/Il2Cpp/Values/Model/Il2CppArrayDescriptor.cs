namespace UnityIl2CppResolver.Il2Cpp.Values.Model;

/// <summary>
/// Describes one validated live single-dimensional zero-based IL2CPP array instance.
/// The descriptor combines runtime-reported length and element information with the x64 vector-data address used by read-only element access.
/// </summary>
internal sealed class Il2CppArrayDescriptor
{
    /// <summary>Gets the remote <c>Il2CppArray*</c> address represented by this descriptor.</summary>
    public nint ArrayAddress { get; }
    /// <summary>Gets the validated logical element count.</summary>
    public int Length { get; }
    /// <summary>Gets the native byte size occupied by one array element.</summary>
    public int ElementSize { get; }
    /// <summary>Gets the remote address containing the first vector element.</summary>
    public nint DataAddress { get; }
    /// <summary>Gets the canonical <c>Il2CppType*</c> address of the array element type.</summary>
    public nint ElementTypeAddress { get; }
    /// <summary>Gets the semantic managed element type name.</summary>
    public string ElementTypeName { get; }

    /// <summary>Initializes one immutable validated array descriptor.</summary>
    /// <param name="arrayAddress">The remote array identity.</param>
    /// <param name="length">The validated logical element count.</param>
    /// <param name="elementSize">The native element size.</param>
    /// <param name="dataAddress">The remote first-element address.</param>
    /// <param name="elementTypeAddress">The canonical element <c>Il2CppType*</c> address.</param>
    /// <param name="elementTypeName">The semantic managed element type name.</param>
    public Il2CppArrayDescriptor(nint arrayAddress, int length, int elementSize, nint dataAddress, nint elementTypeAddress, string elementTypeName)
    {
        if (arrayAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(arrayAddress), "The IL2CPP array address cannot be zero.");

        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementSize);

        if (dataAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(dataAddress), "The IL2CPP array data address cannot be zero.");

        if (elementTypeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(elementTypeAddress), "The IL2CPP array element type address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(elementTypeName);
        ArrayAddress = arrayAddress;
        Length = length;
        ElementSize = elementSize;
        DataAddress = dataAddress;
        ElementTypeAddress = elementTypeAddress;
        ElementTypeName = elementTypeName;
    }
}
