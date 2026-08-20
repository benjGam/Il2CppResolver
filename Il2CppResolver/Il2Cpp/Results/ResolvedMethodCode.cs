namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents the validated executable code mapping associated with a semantically resolved IL2CPP method.
/// This model deliberately remains separate from <see cref="ResolvedMethod"/> because semantic <c>MethodInfo</c> resolution is backend-stable while native code mapping depends on version-sensitive runtime structure layouts.
/// </summary>
public sealed record ResolvedMethodCode
{
    /// <summary>
    /// Gets the semantically resolved IL2CPP method whose native code was mapped.
    /// </summary>
    public ResolvedMethod Method { get; }

    /// <summary>
    /// Gets the validated absolute address of the direct native implementation associated with the method.
    /// </summary>
    public nint NativeAddress { get; }

    /// <summary>
    /// Gets the name of the executable PE section containing the resolved native implementation.
    /// </summary>
    public string SectionName { get; }

    /// <summary>
    /// Gets the name of the compatibility profile used to interpret the underlying IL2CPP <c>MethodInfo</c> structure.
    /// </summary>
    public string CompatibilityProfile { get; }

    /// <summary>
    /// Gets the byte offset within <c>MethodInfo</c> from which the direct native method pointer was read.
    /// This value is retained as resolution evidence rather than hidden as an implementation detail.
    /// </summary>
    public int MethodPointerOffset { get; }

    /// <summary>
    /// Initializes the immutable result of a validated native method-code mapping.
    /// </summary>
    /// <param name="method">The semantically resolved method associated with the native implementation.</param>
    /// <param name="nativeAddress">The validated absolute native implementation address.</param>
    /// <param name="sectionName">The executable PE section containing the native implementation.</param>
    /// <param name="compatibilityProfile">The compatibility profile used to interpret <c>MethodInfo</c>.</param>
    /// <param name="methodPointerOffset">The byte offset used to read the direct method pointer.</param>
    internal ResolvedMethodCode(ResolvedMethod method, nint nativeAddress, string sectionName, string compatibilityProfile, int methodPointerOffset)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(compatibilityProfile);

        if (nativeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(nativeAddress), "The resolved native method address cannot be zero.");

        if (methodPointerOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(methodPointerOffset), "The MethodInfo pointer offset cannot be negative.");

        Method = method;
        NativeAddress = nativeAddress;
        SectionName = sectionName;
        CompatibilityProfile = compatibilityProfile;
        MethodPointerOffset = methodPointerOffset;
    }
}