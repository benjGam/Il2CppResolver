namespace UnityIl2CppResolver.Il2Cpp.Resolution.Model;

/// <summary>
/// Represents the validated concrete storage location of a normal static IL2CPP field.
/// The result preserves the declaring class static-data base, total static-data size, field-relative offset and final absolute process address together with the compatibility profile used to interpret <c>Il2CppClass</c>.
/// Thread-static, literal and instance fields are intentionally outside the scope of this model.
/// </summary>
public sealed record ResolvedFieldStorage
{
    /// <summary>
    /// Gets the semantically resolved normal static field whose concrete storage was located.
    /// </summary>
    public ResolvedField Field { get; }

    /// <summary>
    /// Gets the native base address of the declaring class's IL2CPP <c>static_fields</c> data block.
    /// </summary>
    public nint StaticFieldsAddress { get; }

    /// <summary>
    /// Gets the total size in bytes of the declaring class's normal static-field storage block.
    /// </summary>
    public uint StaticFieldsSize { get; }

    /// <summary>
    /// Gets the field-relative byte offset inside the declaring class's <c>static_fields</c> block.
    /// </summary>
    public nuint StaticStorageOffset { get; }

    /// <summary>
    /// Gets the validated absolute process address containing the static field storage.
    /// This value is calculated as <see cref="StaticFieldsAddress"/> plus <see cref="StaticStorageOffset"/>.
    /// </summary>
    public nint StorageAddress { get; }

    /// <summary>
    /// Gets the compatibility profile used to interpret the declaring runtime <c>Il2CppClass</c>.
    /// </summary>
    public string CompatibilityProfile { get; }

    /// <summary>
    /// Gets the <c>Il2CppClass</c> byte offset from which the <c>static_fields</c> pointer was read.
    /// </summary>
    public int StaticFieldsPointerOffset { get; }

    /// <summary>
    /// Gets the <c>Il2CppClass</c> byte offset from which <c>static_fields_size</c> was read.
    /// </summary>
    public int StaticFieldsSizeOffset { get; }

    /// <summary>
    /// Initializes the immutable result of a validated normal static-field storage resolution.
    /// </summary>
    /// <param name="field">The semantically resolved normal static field.</param>
    /// <param name="staticFieldsAddress">The declaring class's native static-data block address.</param>
    /// <param name="staticFieldsSize">The complete static-data block size in bytes.</param>
    /// <param name="staticStorageOffset">The field-relative offset inside the static-data block.</param>
    /// <param name="storageAddress">The calculated absolute field storage address.</param>
    /// <param name="compatibilityProfile">The compatibility profile used to interpret <c>Il2CppClass</c>.</param>
    /// <param name="staticFieldsPointerOffset">The layout offset used to read <c>static_fields</c>.</param>
    /// <param name="staticFieldsSizeOffset">The layout offset used to read <c>static_fields_size</c>.</param>
    internal ResolvedFieldStorage(ResolvedField field, nint staticFieldsAddress, uint staticFieldsSize, nuint staticStorageOffset, nint storageAddress, string compatibilityProfile, int staticFieldsPointerOffset, int staticFieldsSizeOffset)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(compatibilityProfile);

        if (field.StorageKind != FieldStorageKind.Static)
            throw new ArgumentException("Resolved field storage can only represent a normal static field.", nameof(field));

        if (staticFieldsAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(staticFieldsAddress), "The IL2CPP static-fields address cannot be zero.");

        if (storageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(storageAddress), "The resolved field storage address cannot be zero.");

        if (staticFieldsSize == 0)
            throw new ArgumentOutOfRangeException(nameof(staticFieldsSize), "The IL2CPP static-fields block size cannot be zero.");

        if ((ulong)staticStorageOffset >= staticFieldsSize)
            throw new ArgumentOutOfRangeException(nameof(staticStorageOffset), "The static field offset must belong to the declaring class static-fields block.");

        Field = field;
        StaticFieldsAddress = staticFieldsAddress;
        StaticFieldsSize = staticFieldsSize;
        StaticStorageOffset = staticStorageOffset;
        StorageAddress = storageAddress;
        CompatibilityProfile = compatibilityProfile;
        StaticFieldsPointerOffset = staticFieldsPointerOffset;
        StaticFieldsSizeOffset = staticFieldsSizeOffset;
    }
}