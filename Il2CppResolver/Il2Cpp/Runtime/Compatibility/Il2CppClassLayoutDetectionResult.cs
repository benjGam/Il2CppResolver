namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Represents the evidence produced when one IL2CPP <c>Il2CppClass</c> compatibility profile has been uniquely validated against multiple runtime classes containing normal static fields.
/// The result preserves the selected layout together with the number of independent classes and fields that supported the decision.
/// </summary>
internal sealed record Il2CppClassLayoutDetectionResult
{
    /// <summary>
    /// Gets the unique class-layout compatibility profile accepted by validation.
    /// </summary>
    public Il2CppClassLayout Layout { get; }

    /// <summary>
    /// Gets the number of distinct runtime <c>Il2CppClass</c> instances successfully validated against the selected layout.
    /// </summary>
    public int ValidatedClassCount { get; }

    /// <summary>
    /// Gets the number of normal static fields whose offsets were successfully validated against their declaring class static-data blocks.
    /// </summary>
    public int ValidatedFieldCount { get; }

    /// <summary>
    /// Initializes the immutable result of a successful IL2CPP class-layout detection.
    /// </summary>
    /// <param name="layout">The unique compatibility layout accepted by validation.</param>
    /// <param name="validatedClassCount">The number of distinct runtime classes supporting the layout.</param>
    /// <param name="validatedFieldCount">The number of static-field offsets supporting the layout.</param>
    internal Il2CppClassLayoutDetectionResult(Il2CppClassLayout layout, int validatedClassCount, int validatedFieldCount)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validatedClassCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validatedFieldCount);

        Layout = layout;
        ValidatedClassCount = validatedClassCount;
        ValidatedFieldCount = validatedFieldCount;
    }
}