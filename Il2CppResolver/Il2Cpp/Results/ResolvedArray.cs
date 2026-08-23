using UnityIl2CppResolver.Il2Cpp.Navigation;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents one validated live single-dimensional zero-based IL2CPP array and binds element reads to the resolver session that produced it.
/// Array identity and shape are immutable snapshots, while element access remains generation-aware so stale navigation objects cannot read through invalidated session state.
/// </summary>
public sealed class ResolvedArray
{
    /// <summary>Provides session-bound element reading and generation validation.</summary>
    private readonly IResolutionNavigator _navigator;
    /// <summary>Identifies the cache generation in which this array wrapper was created.</summary>
    private readonly long _generation;
    /// <summary>Gets the remote address containing the first vector element.</summary>
    internal nint DataAddress { get; }
    /// <summary>Gets the canonical <c>Il2CppType*</c> address of the array element type.</summary>
    internal nint RuntimeElementTypeAddress { get; }
    /// <summary>Gets the native byte size occupied by one element.</summary>
    internal int ElementSize { get; }

    /// <summary>Gets the remote <c>Il2CppArray*</c> address represented by this result.</summary>
    public nint Address { get; }
    /// <summary>Gets the validated logical element count.</summary>
    public int Length { get; }
    /// <summary>Gets the semantic managed element type name.</summary>
    public string ElementTypeName { get; }

    /// <summary>Initializes one immutable session-bound array result.</summary>
    /// <param name="address">The remote array identity.</param>
    /// <param name="length">The validated logical element count.</param>
    /// <param name="elementTypeName">The semantic managed element type name.</param>
    /// <param name="dataAddress">The remote first-element address.</param>
    /// <param name="runtimeElementTypeAddress">The canonical element <c>Il2CppType*</c> address.</param>
    /// <param name="elementSize">The native element size.</param>
    /// <param name="navigator">The owning session navigator.</param>
    /// <param name="generation">The cache generation that produced the result.</param>
    internal ResolvedArray(nint address, int length, string elementTypeName, nint dataAddress, nint runtimeElementTypeAddress, int elementSize, IResolutionNavigator navigator, long generation)
    {
        if (address == 0)
            throw new ArgumentOutOfRangeException(nameof(address), "The IL2CPP array address cannot be zero.");

        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementTypeName);

        if (dataAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(dataAddress), "The IL2CPP array data address cannot be zero.");

        if (runtimeElementTypeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(runtimeElementTypeAddress), "The IL2CPP array element type address cannot be zero.");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementSize);
        ArgumentNullException.ThrowIfNull(navigator);
        Address = address;
        Length = length;
        ElementTypeName = elementTypeName;
        DataAddress = dataAddress;
        RuntimeElementTypeAddress = runtimeElementTypeAddress;
        ElementSize = elementSize;
        _navigator = navigator;
        _generation = generation;
    }

    /// <summary>Reads one explicitly supported scalar element.</summary>
    /// <typeparam name="T">The exact supported scalar type expected by the array element.</typeparam>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The validated scalar element value.</returns>
    public T Read<T>(int index) where T : unmanaged => _navigator.ReadArrayElement<T>(this, index, _generation);

    /// <summary>Reads one array element as the exact managed enum type represented by the IL2CPP element type.</summary>
    /// <typeparam name="TEnum">The exact managed enum type.</typeparam>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The validated enum element value.</returns>
    public TEnum ReadEnum<TEnum>(int index) where TEnum : unmanaged, Enum => _navigator.ReadArrayElementEnum<TEnum>(this, index, _generation);

    /// <summary>Reads one array element as a managed object reference.</summary>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The remote <c>Il2CppObject*</c> address, or zero for a null element.</returns>
    public nint ReadReference(int index) => _navigator.ReadArrayElementReference(this, index, _generation);

    /// <summary>Reads one array element as a managed <c>System.String</c>.</summary>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The decoded string, <see cref="string.Empty"/> for an empty string, or <see langword="null"/> for a null element.</returns>
    public string? ReadString(int index) => _navigator.ReadArrayElementString(this, index, _generation);

    /// <summary>Reads one explicitly validated blittable value-type element without managed marshalling.</summary>
    /// <typeparam name="T">The unmanaged managed type whose semantic identity and native size must match the IL2CPP element type.</typeparam>
    /// <param name="index">The zero-based element index.</param>
    /// <returns>The raw blittable value reconstructed from target memory.</returns>
    public T ReadBlittable<T>(int index) where T : unmanaged => _navigator.ReadArrayElementBlittable<T>(this, index, _generation);
}
