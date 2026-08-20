using System.Buffers.Binary;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Native.Remote;

namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Provides low-level semantic access to the native IL2CPP runtime exposed by a validated target process.
/// This class belongs above the generic remote execution layer and translates IL2CPP runtime concepts such as domains and assemblies into strongly controlled native calls.
/// It does not perform high-level assembly, type or method resolution; its responsibility is limited to exposing raw runtime entities required by future resolution backends.
/// </summary>
internal sealed class Il2CppRuntime
{
    /// <summary>
    /// Defines a defensive upper bound for the number of assemblies accepted from a target runtime.
    /// This value is not an IL2CPP format limitation; it prevents corrupted or unexpected runtime data from causing unbounded remote memory reads.
    /// </summary>
    private const ulong MaximumAssemblyCount = 65536;

    /// <summary>
    /// Defines the maximum number of bytes accepted for an IL2CPP image name returned by the runtime.
    /// This defensive limit prevents malformed runtime pointers from causing unbounded remote string reads.
    /// </summary>
    private const int MaximumImageNameLength = 1024;

    /// <summary>
    /// Represents the validated IL2CPP target whose native runtime is inspected by this instance.
    /// </summary>
    private readonly Il2CppTarget _target;

    /// <summary>
    /// Contains the validated native IL2CPP entry points required by the current runtime inspection operations.
    /// </summary>
    private readonly Il2CppRuntimeExports _exports;

    /// <summary>
    /// Provides short-lived native function invocation inside the target process.
    /// </summary>
    private readonly RemoteCall _remoteCall;

    /// <summary>
    /// Initializes runtime inspection over an already validated IL2CPP target and runtime export table.
    /// </summary>
    /// <param name="target">The validated IL2CPP target containing the runtime.</param>
    /// <param name="exports">The strongly typed native IL2CPP export table associated with the target.</param>
    public Il2CppRuntime(Il2CppTarget target, Il2CppRuntimeExports exports)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(exports);

        _target = target;
        _exports = exports;
        _remoteCall = new RemoteCall(target.Process);
    }

    /// <summary>
    /// Retrieves the active IL2CPP domain from the target runtime.
    /// </summary>
    /// <param name="timeout">The maximum amount of time allowed for the native runtime call to complete.</param>
    /// <returns>The native <c>Il2CppDomain*</c> address exposed by the target runtime.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the runtime returns a null domain pointer.
    /// </exception>
    public nint GetDomain(TimeSpan timeout)
    {
        RemoteCallResult result = _remoteCall.InvokePointer(_exports.DomainGet, timeout);

        if (result.ReturnValue == 0)
            throw new InvalidDataException("The IL2CPP runtime returned a null domain pointer.");

        return result.ReturnValue;
    }

    /// <summary>
    /// Retrieves a snapshot of the native assembly pointers currently registered in the specified IL2CPP domain.
    /// The method invokes <c>il2cpp_domain_get_assemblies</c>, validates the returned pointer and count, then copies the complete assembly pointer table into local managed memory.
    /// </summary>
    /// <param name="domain">The native <c>Il2CppDomain*</c> address whose assemblies should be enumerated.</param>
    /// <param name="timeout">The maximum amount of time allowed for the native runtime call to complete.</param>
    /// <returns>An immutable snapshot containing the native <c>Il2CppAssembly*</c> addresses registered in the runtime.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="domain"/> is zero.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the runtime returns inconsistent assembly pointer information or an unreasonable assembly count.
    /// </exception>
    public IReadOnlyList<nint> GetAssemblies(nint domain, TimeSpan timeout)
    {
        if (domain == 0)
            throw new ArgumentOutOfRangeException(nameof(domain), "The IL2CPP domain pointer cannot be zero.");

        RemoteCallPointerSizeOutResult result = _remoteCall.InvokePointerWithNuintOutArgument(_exports.DomainGetAssemblies, domain, timeout);
        ulong assemblyCount = (ulong)result.OutValue;

        if (assemblyCount == 0)
            return Array.Empty<nint>();

        if (assemblyCount > MaximumAssemblyCount)
            throw new InvalidDataException($"The IL2CPP runtime reported an unreasonable assembly count of {assemblyCount}.");

        if (result.ReturnValue == 0)
            throw new InvalidDataException($"The IL2CPP runtime reported {assemblyCount} assembly entries but returned a null assembly table pointer.");

        ulong byteCount = checked(assemblyCount * sizeof(long));

        if (byteCount > int.MaxValue)
            throw new InvalidDataException($"The IL2CPP assembly table requires {byteCount} byte(s), which exceeds the supported local buffer size.");

        byte[] data = _target.Memory.ReadBytes(result.ReturnValue, checked((int)byteCount));
        List<nint> assemblies = new(checked((int)assemblyCount));

        for (int index = 0; index < (int)assemblyCount; index++)
        {
            int offset = index * sizeof(long);
            long rawAddress = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset, sizeof(long)));
            nint assembly = (nint)rawAddress;

            if (assembly == 0)
                throw new InvalidDataException($"The IL2CPP assembly table contains a null assembly pointer at index {index}.");

            assemblies.Add(assembly);
        }

        return assemblies.AsReadOnly();
    }

    /// <summary>
    /// Retrieves semantic information for every assembly currently registered in the specified IL2CPP domain.
    /// Each raw <c>Il2CppAssembly*</c> is converted into its associated <c>Il2CppImage*</c> and image name by invoking the corresponding public IL2CPP runtime APIs.
    /// </summary>
    /// <param name="domain">The native <c>Il2CppDomain*</c> address whose assemblies should be inspected.</param>
    /// <param name="timeout">The maximum amount of time allowed for each individual native IL2CPP runtime call to complete.</param>
    /// <returns>An immutable snapshot describing every assembly discovered in the active IL2CPP domain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="domain"/> is zero.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when IL2CPP returns a null image, a null image-name pointer or invalid string data for any discovered assembly.
    /// </exception>
    public IReadOnlyList<Il2CppAssemblyInfo> GetAssemblyInfos(nint domain, TimeSpan timeout)
    {
        IReadOnlyList<nint> assemblies = GetAssemblies(domain, timeout);
        List<Il2CppAssemblyInfo> results = new(assemblies.Count);

        foreach (nint assemblyAddress in assemblies)
        {
            RemoteCallResult imageResult = _remoteCall.InvokePointer(_exports.AssemblyGetImage, assemblyAddress, timeout);

            if (imageResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null image pointer for assembly 0x{assemblyAddress:X}.");

            nint imageAddress = imageResult.ReturnValue;

            RemoteCallResult nameResult = _remoteCall.InvokePointer(_exports.ImageGetName, imageAddress, timeout);

            if (nameResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null image-name pointer for image 0x{imageAddress:X}.");

            string name = _target.Memory.ReadNullTerminatedAscii(nameResult.ReturnValue, MaximumImageNameLength);

            results.Add(new Il2CppAssemblyInfo(assemblyAddress, imageAddress, name));
        }

        return results.AsReadOnly();
    }
}