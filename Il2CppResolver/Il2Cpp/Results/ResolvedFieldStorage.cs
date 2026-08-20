namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents the validated concrete storage location of a normal static IL2CPP field.
/// The result preserves the declaring class static-data base, total static-data size, field-relative offset and final absolute process address together with the mechanism that produced the mapping.
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
    /// </summary>
    public nint StorageAddress { get; }

    /// <summary>
    /// Gets the mechanism used to obtain the declaring class static-data block.
    /// </summary>
    public FieldStorageResolutionSource ResolutionSource { get; }

    /// <summary>
    /// Gets the compatibility profile used to interpret <c>Il2CppClass</c> when <see cref="ResolutionSource"/> is <see cref="FieldStorageResolutionSource.ClassLayout"/>.
    /// Runtime API resolution exposes no structural compatibility profile and therefore returns <see langword="null"/>.
    /// </summary>
    public string? CompatibilityProfile { get; }

    /// <summary>
    /// Gets the structural <c>Il2CppClass</c> offset used to read <c>static_fields</c>, or <see langword="null"/> when storage was resolved through the public runtime API.
    /// </summary>
    public int? StaticFieldsPointerOffset { get; }

    /// <summary>
    /// Gets the structural <c>Il2CppClass</c> offset used to read <c>static_fields_size</c>, or <see langword="null"/> when storage was resolved through the public runtime API.
    /// </summary>
    public int? StaticFieldsSizeOffset { get; }

    /// <summary>
    /// Initializes the immutable result of a validated normal static-field storage resolution.
    /// </summary>
    /// <param name="field">The semantically resolved normal static field.</param>
    /// <param name="staticFieldsAddress">The declaring class's native static-data block address.</param>
    /// <param name="staticFieldsSize">The complete static-data block size in bytes.</param>
    /// <param name="staticStorageOffset">The field-relative offset inside the static-data block.</param>
    /// <param name="storageAddress">The calculated absolute field storage address.</param>
    /// <param name="resolutionSource">The mechanism used to obtain the static-data block.</param>
    /// <param name="compatibilityProfile">The structural compatibility profile when a class layout was used.</param>
    /// <param name="staticFieldsPointerOffset">The structural offset used to read <c>static_fields</c>, when applicable.</param>
    /// <param name="staticFieldsSizeOffset">The structural offset used to read <c>static_fields_size</c>, when applicable.</param>
    internal ResolvedFieldStorage(ResolvedField field, nint staticFieldsAddress, uint staticFieldsSize, nuint staticStorageOffset, nint storageAddress, FieldStorageResolutionSource resolutionSource, string? compatibilityProfile, int? staticFieldsPointerOffset, int? staticFieldsSizeOffset)
    {
        ArgumentNullException.ThrowIfNull(field);

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

        if (resolutionSource != FieldStorageResolutionSource.RuntimeApi && resolutionSource != FieldStorageResolutionSource.ClassLayout)
            throw new ArgumentOutOfRangeException(nameof(resolutionSource), resolutionSource, "Unsupported field-storage resolution source.");

        bool usesLayoutEvidence = resolutionSource == FieldStorageResolutionSource.ClassLayout;

        if (usesLayoutEvidence && string.IsNullOrWhiteSpace(compatibilityProfile))
            throw new ArgumentException("Class-layout resolution must expose a compatibility profile.", nameof(compatibilityProfile));

        if (usesLayoutEvidence && (staticFieldsPointerOffset is null || staticFieldsSizeOffset is null))
            throw new ArgumentException("Class-layout resolution must expose both structural offsets.", nameof(staticFieldsPointerOffset));

        if (!usesLayoutEvidence && (compatibilityProfile is not null || staticFieldsPointerOffset is not null || staticFieldsSizeOffset is not null))
            throw new ArgumentException("Runtime API resolution cannot expose structural class-layout evidence.", nameof(compatibilityProfile));

        Field = field;
        StaticFieldsAddress = staticFieldsAddress;
        StaticFieldsSize = staticFieldsSize;
        StaticStorageOffset = staticStorageOffset;
        StorageAddress = storageAddress;
        ResolutionSource = resolutionSource;
        CompatibilityProfile = compatibilityProfile;
        StaticFieldsPointerOffset = staticFieldsPointerOffset;
        StaticFieldsSizeOffset = staticFieldsSizeOffset;
    }
}
