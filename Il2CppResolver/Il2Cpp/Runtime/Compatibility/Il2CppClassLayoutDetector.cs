using System.ComponentModel;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Il2Cpp.Resolution.Model;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Compatibility;

/// <summary>
/// Detects a compatible IL2CPP <c>Il2CppClass</c> layout by validating multiple structural profiles against normal static fields declared by several independent runtime classes.
/// A candidate survives only when every inspected class exposes a non-null readable <c>static_fields</c> block, a reasonable <c>static_fields_size</c>, and field offsets that remain inside that block.
/// Detection succeeds only when exactly one candidate profile satisfies every invariant, preventing ambiguous layouts from being selected automatically.
/// </summary>
internal sealed class Il2CppClassLayoutDetector
{
    /// <summary>
    /// Defines the defensive maximum static-data block size accepted while validating a class layout.
    /// The limit is intentionally generous while preventing unrelated 32-bit structure fields from being interpreted as multi-gigabyte static storage sizes.
    /// </summary>
    private const uint MaximumStaticFieldsSize = 16 * 1024 * 1024;

    /// <summary>
    /// Represents the validated IL2CPP target whose runtime class structures and memory mappings are inspected.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Contains the explicit class-layout compatibility profiles considered during detection.
    /// Candidate ordering has no influence on the result because ambiguous survivors are rejected.
    /// </summary>
    private readonly IReadOnlyList<Il2CppClassLayout> _candidates;

    /// <summary>
    /// Defines the minimum number of distinct declaring classes required before a class layout can be accepted.
    /// </summary>
    private readonly int _minimumValidatedClassCount;

    /// <summary>
    /// Initializes a class-layout detector for the specified IL2CPP target.
    /// </summary>
    /// <param name="target">The validated IL2CPP target whose class layout should be detected.</param>
    /// <param name="candidates">The explicit structural compatibility profiles available for validation.</param>
    /// <param name="minimumValidatedClassCount">The minimum number of distinct static-field declaring classes required as positive evidence.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> or <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when no candidate profiles are supplied or when candidate names are duplicated.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimumValidatedClassCount"/> is less than one.
    /// </exception>
    public Il2CppClassLayoutDetector(Il2CppTarget target, IReadOnlyList<Il2CppClassLayout> candidates, int minimumValidatedClassCount = 3)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
            throw new ArgumentException("At least one IL2CPP class-layout compatibility profile must be supplied.", nameof(candidates));

        if (minimumValidatedClassCount < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumValidatedClassCount), "At least one independently validated class is required.");

        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (Il2CppClassLayout candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            if (!names.Add(candidate.Name))
                throw new ArgumentException($"Duplicate IL2CPP class-layout compatibility profile name '{candidate.Name}'.", nameof(candidates));
        }

        _target = target;
        _candidates = candidates.ToArray();
        _minimumValidatedClassCount = minimumValidatedClassCount;
    }

    /// <summary>
    /// Detects the unique class-layout compatibility profile supported by the supplied normal static fields.
    /// Evidence must originate from enough distinct declaring classes so repeated fields from one class cannot independently satisfy the configured confidence threshold.
    /// </summary>
    /// <param name="fields">The semantically resolved normal static fields used as structural evidence.</param>
    /// <returns>The unique validated compatibility profile together with its supporting class and field counts.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fields"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when evidence contains a non-static field, lacks a static storage offset or covers too few distinct declaring classes.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when no candidate satisfies every structural invariant or when multiple candidates remain valid.
    /// </exception>
    public Il2CppClassLayoutDetectionResult Detect(IReadOnlyList<ResolvedField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyDictionary<nint, IReadOnlyList<ResolvedField>> classes = BuildEvidence(fields);

        if (classes.Count < _minimumValidatedClassCount)
            throw new ArgumentException($"Class-layout detection requires at least {_minimumValidatedClassCount} distinct declaring classes containing normal static fields, but only {classes.Count} were supplied.", nameof(fields));

        List<Il2CppClassLayoutDetectionResult> matches = new();
        List<string> failures = new();

        foreach (Il2CppClassLayout candidate in _candidates)
        {
            bool valid = TryValidateCandidate(candidate, classes, out int validatedClassCount, out int validatedFieldCount, out string? failureReason);

            if (!valid)
            {
                failures.Add($"{candidate.Name}: {failureReason}");
                continue;
            }

            if (validatedClassCount < _minimumValidatedClassCount)
            {
                failures.Add($"{candidate.Name}: only {validatedClassCount} independent class(es) were validated; {_minimumValidatedClassCount} are required.");
                continue;
            }

            matches.Add(new Il2CppClassLayoutDetectionResult(candidate, validatedClassCount, validatedFieldCount));
        }

        if (matches.Count == 0)
        {
            string diagnostics = failures.Count == 0 ? "No candidate produced validation evidence." : string.Join(" | ", failures);
            throw new InvalidDataException($"No IL2CPP class-layout compatibility profile could be validated. {diagnostics}");
        }

        if (matches.Count > 1)
        {
            string profileNames = string.Join(", ", matches.Select(match => match.Layout.Name));
            throw new InvalidDataException($"IL2CPP class-layout detection is ambiguous. Multiple compatibility profiles passed validation: {profileNames}.");
        }

        return matches[0];
    }

    /// <summary>
    /// Validates one class-layout profile against every supplied declaring class and each normal static field associated with that class.
    /// A single invalid pointer, unreasonable size, unreadable block or out-of-range field offset rejects the candidate immediately.
    /// </summary>
    /// <param name="candidate">The compatibility profile to validate.</param>
    /// <param name="classes">The static-field evidence grouped by native declaring-class address.</param>
    /// <param name="validatedClassCount">Receives the number of independently validated declaring classes.</param>
    /// <param name="validatedFieldCount">Receives the number of validated normal static fields.</param>
    /// <param name="failureReason">Receives a diagnostic explanation when validation fails.</param>
    /// <returns><see langword="true"/> when the candidate satisfies every invariant; otherwise <see langword="false"/>.</returns>
    private bool TryValidateCandidate(Il2CppClassLayout candidate, IReadOnlyDictionary<nint, IReadOnlyList<ResolvedField>> classes, out int validatedClassCount, out int validatedFieldCount, out string? failureReason)
    {
        validatedClassCount = 0;
        validatedFieldCount = 0;
        failureReason = null;

        foreach (KeyValuePair<nint, IReadOnlyList<ResolvedField>> pair in classes)
        {
            nint classAddress = pair.Key;
            IReadOnlyList<ResolvedField> classFields = pair.Value;

            nint staticFieldsPointerAddress;
            nint staticFieldsSizeAddress;

            try
            {
                staticFieldsPointerAddress = AddOffset(classAddress, candidate.StaticFieldsPointerOffset);
                staticFieldsSizeAddress = AddOffset(classAddress, candidate.StaticFieldsSizeOffset);
            }
            catch (OverflowException)
            {
                failureReason = $"Class 0x{classAddress:X} overflows when applying profile '{candidate.Name}'.";
                return false;
            }

            try
            {
                if (!_target.Memory.IsReadableRange(staticFieldsPointerAddress, (nuint)IntPtr.Size))
                {
                    failureReason = $"Class 0x{classAddress:X} exposes an unreadable static_fields slot at 0x{staticFieldsPointerAddress:X}.";
                    return false;
                }

                if (!_target.Memory.IsReadableRange(staticFieldsSizeAddress, sizeof(uint)))
                {
                    failureReason = $"Class 0x{classAddress:X} exposes an unreadable static_fields_size slot at 0x{staticFieldsSizeAddress:X}.";
                    return false;
                }

                nint staticFieldsAddress = _target.Memory.ReadPointer(staticFieldsPointerAddress);
                uint staticFieldsSize = _target.Memory.Read<uint>(staticFieldsSizeAddress);

                if (staticFieldsAddress == 0)
                {
                    failureReason = $"Class 0x{classAddress:X} produced a null static_fields pointer.";
                    return false;
                }

                if (staticFieldsSize == 0)
                {
                    failureReason = $"Class 0x{classAddress:X} produced a zero static_fields_size despite containing normal static field evidence.";
                    return false;
                }

                if (staticFieldsSize > MaximumStaticFieldsSize)
                {
                    failureReason = $"Class 0x{classAddress:X} produced unreasonable static_fields_size 0x{staticFieldsSize:X}.";
                    return false;
                }

                if (!_target.Memory.IsReadableRange(staticFieldsAddress, staticFieldsSize))
                {
                    failureReason = $"Class 0x{classAddress:X} produced unreadable static-fields block 0x{staticFieldsAddress:X} with size 0x{staticFieldsSize:X}.";
                    return false;
                }

                foreach (ResolvedField field in classFields)
                {
                    if (field.StaticStorageOffset is not nuint staticStorageOffset)
                    {
                        failureReason = $"Field '{field.Query.Name}' does not expose a normal static storage offset.";
                        return false;
                    }

                    if ((ulong)staticStorageOffset >= staticFieldsSize)
                    {
                        failureReason = $"Field '{field.Query.Name}' on class 0x{classAddress:X} has static offset 0x{staticStorageOffset:X}, outside block size 0x{staticFieldsSize:X}.";
                        return false;
                    }

                    nint storageAddress = AddOffset(staticFieldsAddress, staticStorageOffset);

                    if (!_target.Memory.IsReadableRange(storageAddress, 1))
                    {
                        failureReason = $"Field '{field.Query.Name}' on class 0x{classAddress:X} resolves to unreadable storage address 0x{storageAddress:X}.";
                        return false;
                    }

                    validatedFieldCount++;
                }
            }
            catch (Win32Exception exception)
            {
                failureReason = $"Native memory validation failed for class 0x{classAddress:X}: {exception.Message}";
                return false;
            }

            validatedClassCount++;
        }

        return true;
    }

    /// <summary>
    /// Groups normal static-field evidence by declaring runtime class and removes duplicate <c>FieldInfo*</c> identities.
    /// </summary>
    /// <param name="fields">The semantic field-resolution results supplied as layout evidence.</param>
    /// <returns>A read-only mapping from native <c>Il2CppClass*</c> addresses to their distinct normal static fields.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when evidence contains a field that is not a normal static field.
    /// </exception>
    private static IReadOnlyDictionary<nint, IReadOnlyList<ResolvedField>> BuildEvidence(IReadOnlyList<ResolvedField> fields)
    {
        Dictionary<nint, List<ResolvedField>> groupedFields = new();
        HashSet<nint> fieldAddresses = new();

        foreach (ResolvedField field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);

            if (field.StorageKind != FieldStorageKind.Static)
                throw new ArgumentException($"Field '{field.Query.Name}' uses storage kind '{field.StorageKind}' and cannot serve as normal static-field layout evidence.", nameof(fields));

            if (field.StaticStorageOffset is null)
                throw new ArgumentException($"Field '{field.Query.Name}' does not expose a normal static storage offset.", nameof(fields));

            if (!fieldAddresses.Add(field.FieldInfoAddress))
                continue;

            nint classAddress = field.DeclaringType.ClassAddress;

            if (!groupedFields.TryGetValue(classAddress, out List<ResolvedField>? classFields))
            {
                classFields = new List<ResolvedField>();
                groupedFields.Add(classAddress, classFields);
            }

            classFields.Add(field);
        }

        return groupedFields.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ResolvedField>)pair.Value.AsReadOnly());
    }

    /// <summary>
    /// Adds a signed structural offset to a native address while preserving overflow detection.
    /// </summary>
    /// <param name="baseAddress">The native base address receiving the structural offset.</param>
    /// <param name="offset">The byte offset to add.</param>
    /// <returns>The resulting native address.</returns>
    private static nint AddOffset(nint baseAddress, int offset)
    {
        return checked((nint)(baseAddress.ToInt64() + offset));
    }

    /// <summary>
    /// Adds a pointer-sized unsigned runtime offset to a native address while preserving overflow detection.
    /// </summary>
    /// <param name="baseAddress">The native base address receiving the runtime offset.</param>
    /// <param name="offset">The pointer-sized byte offset to add.</param>
    /// <returns>The resulting native address.</returns>
    /// <exception cref="OverflowException">
    /// Thrown when the resulting address cannot be represented by the supported Windows x64 address model.
    /// </exception>
    private static nint AddOffset(nint baseAddress, nuint offset)
    {
        ulong baseValue = unchecked((ulong)(nuint)baseAddress);
        ulong offsetValue = (ulong)offset;

        if (offsetValue > ulong.MaxValue - baseValue)
            throw new OverflowException();

        ulong result = baseValue + offsetValue;

        if (result > long.MaxValue)
            throw new OverflowException();

        return (nint)(long)result;
    }
}