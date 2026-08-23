using System.ComponentModel;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Layouts;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Detection;

/// <summary>
/// Detects a compatible IL2CPP <c>MethodInfo</c> structural layout by validating multiple candidate pointer offsets against live runtime methods.
/// A candidate survives only when every non-null candidate pointer resolves to executable code inside <c>GameAssembly.dll</c>, and detection succeeds only when one structurally unique candidate reaches the configured positive-evidence threshold.
/// </summary>
internal sealed class Il2CppMethodInfoLayoutDetector
{
    /// <summary>
    /// Represents the validated IL2CPP target whose method structures and PE executable sections are inspected.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Contains the structurally unique compatibility profiles considered during detection.
    /// </summary>
    private readonly IReadOnlyList<Il2CppMethodInfoLayout> _candidates;

    /// <summary>
    /// Defines the minimum number of executable candidate pointers required before a layout can be accepted.
    /// </summary>
    private readonly int _minimumValidatedMethodCount;

    /// <summary>
    /// Initializes a MethodInfo-layout detector for the specified target.
    /// </summary>
    /// <param name="target">The validated IL2CPP target whose method layout should be detected.</param>
    /// <param name="candidates">The structural compatibility profiles available for validation.</param>
    /// <param name="minimumValidatedMethodCount">The minimum number of executable method pointers required as positive evidence.</param>
    /// <exception cref="ArgumentException">Thrown when no candidates are supplied or when names or structural offsets are duplicated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumValidatedMethodCount"/> is less than one.</exception>
    public Il2CppMethodInfoLayoutDetector(Il2CppTarget target, IReadOnlyList<Il2CppMethodInfoLayout> candidates, int minimumValidatedMethodCount = 5)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
            throw new ArgumentException("At least one IL2CPP MethodInfo compatibility profile must be supplied.", nameof(candidates));

        if (minimumValidatedMethodCount < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumValidatedMethodCount), "At least one validated method is required for layout detection.");

        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<int> offsets = new();

        foreach (Il2CppMethodInfoLayout candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            if (!names.Add(candidate.Name))
                throw new ArgumentException($"Duplicate IL2CPP MethodInfo compatibility profile name '{candidate.Name}'.", nameof(candidates));

            if (!offsets.Add(candidate.DirectMethodPointerOffset))
                throw new ArgumentException($"MethodInfo compatibility profile '{candidate.Name}' duplicates an existing structural pointer offset 0x{candidate.DirectMethodPointerOffset:X}.", nameof(candidates));
        }

        _target = target;
        _candidates = candidates.ToArray();
        _minimumValidatedMethodCount = minimumValidatedMethodCount;
    }

    /// <summary>
    /// Detects the unique compatibility profile supported by the supplied runtime <c>MethodInfo*</c> addresses.
    /// </summary>
    /// <param name="methodAddresses">The runtime method identities used as structural evidence.</param>
    /// <returns>The unique validated layout together with positive and null-pointer evidence counts.</returns>
    /// <exception cref="ArgumentException">Thrown when no usable method addresses are supplied.</exception>
    /// <exception cref="InvalidDataException">Thrown when no profile survives or multiple profiles remain valid.</exception>
    public Il2CppMethodInfoLayoutDetectionResult Detect(IReadOnlyList<nint> methodAddresses)
    {
        ArgumentNullException.ThrowIfNull(methodAddresses);
        IReadOnlyList<nint> distinctMethods = GetDistinctMethodAddresses(methodAddresses);

        if (distinctMethods.Count == 0)
            throw new ArgumentException("MethodInfo layout detection requires at least one runtime method address.", nameof(methodAddresses));

        List<Il2CppMethodInfoLayoutDetectionResult> matches = new();
        List<string> failures = new();

        foreach (Il2CppMethodInfoLayout candidate in _candidates)
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
            string profileNames = string.Join(", ", matches.Select(static match => match.Layout.Name));
            throw new InvalidDataException($"IL2CPP MethodInfo layout detection is ambiguous. Multiple compatibility profiles passed validation: {profileNames}.");
        }

        return matches[0];
    }

    /// <summary>
    /// Validates one compatibility profile against every supplied runtime method address.
    /// An unreadable candidate slot rejects only that candidate; process-termination and other non-memory failures continue to propagate normally.
    /// </summary>
    /// <param name="candidate">The compatibility profile to validate.</param>
    /// <param name="methodAddresses">The distinct runtime <c>MethodInfo*</c> addresses used as evidence.</param>
    /// <param name="validatedMethodCount">Receives the number of executable pointers accepted as positive evidence.</param>
    /// <param name="nullMethodPointerCount">Receives the number of null candidate pointers ignored as absence of evidence.</param>
    /// <param name="failureReason">Receives a diagnostic explanation when validation rejects the candidate.</param>
    /// <returns><see langword="true"/> when the candidate contains no invalid non-null pointer; otherwise <see langword="false"/>.</returns>
    private bool TryValidateCandidate(Il2CppMethodInfoLayout candidate, IReadOnlyList<nint> methodAddresses, out int validatedMethodCount, out int nullMethodPointerCount, out string? failureReason)
    {
        validatedMethodCount = 0;
        nullMethodPointerCount = 0;
        failureReason = null;

        foreach (nint methodAddress in methodAddresses)
        {
            nint pointerAddress;

            try
            {
                pointerAddress = checked((nint)(methodAddress.ToInt64() + candidate.DirectMethodPointerOffset));
            }
            catch (OverflowException)
            {
                failureReason = $"MethodInfo 0x{methodAddress:X} overflows when applying pointer offset 0x{candidate.DirectMethodPointerOffset:X}.";
                return false;
            }

            nint nativeAddress;

            try
            {
                nativeAddress = _target.Memory.ReadPointer(pointerAddress);
            }
            catch (Win32Exception exception)
            {
                failureReason = $"MethodInfo 0x{methodAddress:X} could not be read at offset 0x{candidate.DirectMethodPointerOffset:X}: {exception.Message}";
                return false;
            }

            if (nativeAddress == 0)
            {
                nullMethodPointerCount++;
                continue;
            }

            if (!_target.GameAssemblyImage.ContainsAddress(nativeAddress))
            {
                failureReason = $"MethodInfo 0x{methodAddress:X} produced pointer 0x{nativeAddress:X}, which is outside GameAssembly.";
                return false;
            }

            PeSection? section = _target.GameAssemblyImage.FindSectionContainingAddress(nativeAddress);

            if (section is null)
            {
                failureReason = $"MethodInfo 0x{methodAddress:X} produced pointer 0x{nativeAddress:X}, which cannot be associated with a parsed PE section.";
                return false;
            }

            if (!section.IsExecutable)
            {
                failureReason = $"MethodInfo 0x{methodAddress:X} produced pointer 0x{nativeAddress:X} inside non-executable section '{section.Name}'.";
                return false;
            }

            validatedMethodCount++;
        }

        return true;
    }

    /// <summary>
    /// Removes duplicate native method identities while preserving discovery order.
    /// </summary>
    /// <param name="methodAddresses">The runtime method addresses supplied as evidence.</param>
    /// <returns>An immutable sequence containing each distinct non-zero method address once.</returns>
    private static IReadOnlyList<nint> GetDistinctMethodAddresses(IReadOnlyList<nint> methodAddresses)
    {
        HashSet<nint> addresses = new();
        List<nint> results = new(methodAddresses.Count);

        foreach (nint methodAddress in methodAddresses)
        {
            if (methodAddress == 0)
                throw new ArgumentException("Layout detection cannot use a null MethodInfo address.", nameof(methodAddresses));

            if (addresses.Add(methodAddress))
                results.Add(methodAddress);
        }

        return results.AsReadOnly();
    }
}
