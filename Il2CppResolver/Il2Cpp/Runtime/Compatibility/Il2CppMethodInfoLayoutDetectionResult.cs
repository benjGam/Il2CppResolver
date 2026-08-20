namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Represents the evidence produced when an IL2CPP <c>MethodInfo</c> compatibility layout has been validated against multiple runtime methods.
/// The result records the selected layout together with the amount of executable-pointer evidence that supported the selection.
/// </summary>
internal sealed record Il2CppMethodInfoLayoutDetectionResult
{
    /// <summary>
    /// Gets the unique compatibility layout that satisfied every validation invariant.
    /// </summary>
    public IIl2CppMethodInfoLayout Layout { get; }

    /// <summary>
    /// Gets the number of distinct <c>MethodInfo</c> instances whose candidate direct method pointer resolved to an executable section inside <c>GameAssembly.dll</c>.
    /// </summary>
    public int ValidatedMethodCount { get; }

    /// <summary>
    /// Gets the number of inspected <c>MethodInfo</c> instances whose candidate direct method pointer was null.
    /// Null pointers provide no positive evidence and are ignored rather than treated as layout failures.
    /// </summary>
    public int NullMethodPointerCount { get; }

    /// <summary>
    /// Initializes the immutable evidence associated with a successful compatibility-layout detection.
    /// </summary>
    /// <param name="layout">The unique compatibility layout accepted by validation.</param>
    /// <param name="validatedMethodCount">The number of executable method pointers supporting the layout.</param>
    /// <param name="nullMethodPointerCount">The number of null candidate pointers ignored during validation.</param>
    internal Il2CppMethodInfoLayoutDetectionResult(IIl2CppMethodInfoLayout layout, int validatedMethodCount, int nullMethodPointerCount)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegative(validatedMethodCount);
        ArgumentOutOfRangeException.ThrowIfNegative(nullMethodPointerCount);

        Layout = layout;
        ValidatedMethodCount = validatedMethodCount;
        NullMethodPointerCount = nullMethodPointerCount;
    }
}