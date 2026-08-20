using System.ComponentModel;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Detects a compatible IL2CPP <c>MethodInfo</c> layout by validating candidate layouts against multiple independently discovered runtime methods.
/// A candidate is accepted only when enough non-null direct method pointers resolve inside executable sections of <c>GameAssembly.dll</c> and no non-null candidate violates those invariants.
/// Detection succeeds only when exactly one compatibility profile remains valid, preventing ambiguous structural assumptions from being selected automatically.
/// </summary>
internal sealed class Il2CppMethodInfoLayoutDetector
{
    /// <summary>
    /// Represents the validated IL2CPP target whose runtime structures and native PE image are inspected during layout detection.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Contains the explicit compatibility profiles considered by the detector.
    /// Candidate ordering has no effect on selection because ambiguous successful profiles are rejected.
    /// </summary>
    private readonly IReadOnlyList<IIl2CppMethodInfoLayout> _candidates;

    /// <summary>
    /// Defines the minimum amount of positive executable-pointer evidence required before a compatibility profile can be accepted.
    /// </summary>
    private readonly int _minimumValidatedMethodCount;

    /// <summary>
    /// Initializes a compatibility-layout detector for the specified IL2CPP target.
    /// </summary>
    /// <param name="target">The validated IL2CPP target whose runtime layout should be detected.</param>
    /// <param name="candidates">The explicit compatibility profiles available for validation.</param>
    /// <param name="minimumValidatedMethodCount">The minimum number of distinct executable method pointers required to accept a profile.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> or <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when no candidate profiles are supplied, when candidate names are duplicated or when a candidate exposes an invalid pointer offset.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimumValidatedMethodCount"/> is less than one.
    /// </exception>
    public Il2CppMethodInfoLayoutDetector(Il2CppTarget target, IReadOnlyList<IIl2CppMethodInfoLayout> candidates, int minimumValidatedMethodCount = 5)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
            throw new ArgumentException("At least one IL2CPP MethodInfo compatibility profile must be supplied.", nameof(candidates));

        if (minimumValidatedMethodCount < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumValidatedMethodCount), "At least one validated method is required for layout detection.");

        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (IIl2CppMethodInfoLayout candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            if (!names.Add(candidate.Name))
                throw new ArgumentException($"Duplicate IL2CPP MethodInfo compatibility profile name '{candidate.Name}'.", nameof(candidates));

            if (candidate.DirectMethodPointerOffset < 0)
                throw new ArgumentException($"Compatibility profile '{candidate.Name}' exposes a negative direct method pointer offset.", nameof(candidates));

            if (candidate.DirectMethodPointerOffset % IntPtr.Size != 0)
                throw new ArgumentException($"Compatibility profile '{candidate.Name}' exposes unaligned direct method pointer offset 0x{candidate.DirectMethodPointerOffset:X}.", nameof(candidates));
        }

        _target = target;
        _candidates = candidates.ToArray();
        _minimumValidatedMethodCount = minimumValidatedMethodCount;
    }

    /// <summary>
    /// Detects the unique compatibility layout supported by a collection of independently discovered runtime methods.
    /// Duplicate <c>MethodInfo</c> addresses are removed before validation so repeated references cannot artificially increase the amount of evidence supporting a candidate.
    /// </summary>
    /// <param name="methods">The runtime methods used as structural validation evidence.</param>
    /// <returns>The unique validated compatibility layout together with its supporting evidence counts.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="methods"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when too few distinct runtime methods are supplied to satisfy the configured validation threshold.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when no candidate layout satisfies the validation invariants or when multiple candidates remain valid and the layout is therefore ambiguous.
    /// </exception>
    public Il2CppMethodInfoLayoutDetectionResult Detect(IReadOnlyList<Il2CppMethodInfo> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        IReadOnlyList<Il2CppMethodInfo> distinctMethods = GetDistinctMethods(methods);

        if (distinctMethods.Count < _minimumValidatedMethodCount)
            throw new ArgumentException($"Layout detection requires at least {_minimumValidatedMethodCount} distinct runtime methods, but only {distinctMethods.Count} were supplied.", nameof(methods));

        List<Il2CppMethodInfoLayoutDetectionResult> matches = new();
        List<string> failures = new();

        foreach (IIl2CppMethodInfoLayout candidate in _candidates)
        {
            bool valid = TryValidateCandidate(candidate, distinctMethods, out int validatedMethodCount, out int nullMethodPointerCount, out string? failureReason);

            if (!valid)
            {
                failures.Add($"{candidate.Name}: {failureReason}");
                continue;
            }

            if (validatedMethodCount < _minimumValidatedMethodCount)
            {
                failures.Add($"{candidate.Name}: only {validatedMethodCount} executable method pointer(s) were validated; {_minimumValidatedMethodCount} are required.");
                continue;
            }

            matches.Add(new Il2CppMethodInfoLayoutDetectionResult(candidate, validatedMethodCount, nullMethodPointerCount));
        }

        if (matches.Count == 0)
        {
            string diagnostics = failures.Count == 0 ? "No candidate produced validation evidence." : string.Join(" | ", failures);
            throw new InvalidDataException($"No IL2CPP MethodInfo compatibility profile could be validated. {diagnostics}");
        }

        if (matches.Count > 1)
        {
            string profileNames = string.Join(", ", matches.Select(match => match.Layout.Name));
            throw new InvalidDataException($"IL2CPP MethodInfo layout detection is ambiguous. Multiple compatibility profiles passed validation: {profileNames}.");
        }

        return matches[0];
    }

    /// <summary>
    /// Validates one compatibility profile against every supplied runtime method.
    /// Null candidate method pointers are treated as absence of evidence, while any non-null pointer that does not resolve to executable code inside <c>GameAssembly.dll</c> immediately rejects the profile.
    /// </summary>
    /// <param name="candidate">The compatibility profile to validate.</param>
    /// <param name="methods">The distinct runtime methods used as structural evidence.</param>
    /// <param name="validatedMethodCount">Receives the number of candidate pointers validated as executable code.</param>
    /// <param name="nullMethodPointerCount">Receives the number of candidate pointers that were null and therefore ignored.</param>
    /// <param name="failureReason">Receives a diagnostic description when validation fails.</param>
    /// <returns><see langword="true"/> when the candidate contains no invalid non-null pointer; otherwise <see langword="false"/>.</returns>
    private bool TryValidateCandidate(IIl2CppMethodInfoLayout candidate, IReadOnlyList<Il2CppMethodInfo> methods, out int validatedMethodCount, out int nullMethodPointerCount, out string? failureReason)
    {
        validatedMethodCount = 0;
        nullMethodPointerCount = 0;
        failureReason = null;

        foreach (Il2CppMethodInfo method in methods)
        {
            nint pointerAddress;

            try
            {
                pointerAddress = checked((nint)(method.MethodAddress.ToInt64() + candidate.DirectMethodPointerOffset));
            }
            catch (OverflowException)
            {
                failureReason = $"MethodInfo 0x{method.MethodAddress:X} overflows when applying pointer offset 0x{candidate.DirectMethodPointerOffset:X}.";
                return false;
            }

            nint nativeAddress;

            try
            {
                nativeAddress = _target.Memory.ReadPointer(pointerAddress);
            }
            catch (Win32Exception exception)
            {
                failureReason = $"MethodInfo 0x{method.MethodAddress:X} could not be read at offset 0x{candidate.DirectMethodPointerOffset:X}: {exception.Message}";
                return false;
            }

            if (nativeAddress == 0)
            {
                nullMethodPointerCount++;
                continue;
            }

            if (!_target.GameAssemblyImage.ContainsAddress(nativeAddress))
            {
                failureReason = $"Method '{method.Name}' at MethodInfo 0x{method.MethodAddress:X} produced pointer 0x{nativeAddress:X}, which is outside GameAssembly.";
                return false;
            }

            PeSection? section = _target.GameAssemblyImage.FindSectionContainingAddress(nativeAddress);

            if (section is null)
            {
                failureReason = $"Method '{method.Name}' at MethodInfo 0x{method.MethodAddress:X} produced pointer 0x{nativeAddress:X}, which cannot be associated with a parsed PE section.";
                return false;
            }

            if (!section.IsExecutable)
            {
                failureReason = $"Method '{method.Name}' at MethodInfo 0x{method.MethodAddress:X} produced pointer 0x{nativeAddress:X} inside non-executable section '{section.Name}'.";
                return false;
            }

            validatedMethodCount++;
        }

        return true;
    }

    /// <summary>
    /// Removes duplicate runtime method identities while preserving their original discovery order.
    /// This prevents repeated references to the same <c>MethodInfo</c> from artificially satisfying the multi-method validation threshold.
    /// </summary>
    /// <param name="methods">The runtime method collection supplied to layout detection.</param>
    /// <returns>An immutable sequence containing one entry for each distinct native <c>MethodInfo*</c> address.</returns>
    private static IReadOnlyList<Il2CppMethodInfo> GetDistinctMethods(IReadOnlyList<Il2CppMethodInfo> methods)
    {
        HashSet<nint> addresses = new();
        List<Il2CppMethodInfo> results = new(methods.Count);

        foreach (Il2CppMethodInfo method in methods)
        {
            ArgumentNullException.ThrowIfNull(method);

            if (addresses.Add(method.MethodAddress))
                results.Add(method);
        }

        return results.AsReadOnly();
    }
}