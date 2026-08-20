using System.Buffers.Binary;
using System.Text;
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
    /// Defines the defensive maximum number of methods accepted while enumerating a single IL2CPP class.
    /// </summary>
    private const int MaximumMethodCount = 65536;

    /// <summary>
    /// Defines the defensive maximum parameter count accepted for a single IL2CPP method.
    /// </summary>
    private const uint MaximumMethodParameterCount = 1024;

    /// <summary>
    /// Defines the maximum UTF-8 byte length accepted for runtime method and type names.
    /// </summary>
    private const int MaximumRuntimeNameLength = 4096;

    /// <summary>
    /// Defines the defensive maximum number of fields accepted while enumerating a single IL2CPP class.
    /// </summary>
    private const int MaximumFieldCount = 65536;

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

    /// <summary>
    /// Resolves an IL2CPP class from an image, namespace and type name through the public runtime API.
    /// Namespace and type identifiers are encoded as null-terminated UTF-8 strings and exist inside the target process only for the duration of the native call.
    /// </summary>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> containing the requested type.</param>
    /// <param name="namespaceName">The managed namespace containing the requested type. An empty namespace is valid.</param>
    /// <param name="typeName">The managed type name to resolve.</param>
    /// <param name="timeout">The maximum amount of time allowed for the native runtime call to complete.</param>
    /// <returns>The native <c>Il2CppClass*</c> address when the class exists, or zero when IL2CPP cannot resolve the requested type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="imageAddress"/> is zero.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="namespaceName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="typeName"/> is empty or when either managed identifier contains an embedded null character.
    /// </exception>
    public nint GetClass(nint imageAddress, string namespaceName, string typeName, TimeSpan timeout)
    {
        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image pointer cannot be zero.");

        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        byte[] namespaceBuffer = EncodeNullTerminatedUtf8(namespaceName, nameof(namespaceName));
        byte[] typeNameBuffer = EncodeNullTerminatedUtf8(typeName, nameof(typeName));

        RemoteCallResult result = _remoteCall.InvokePointerWithBufferArguments(_exports.ClassFromName, imageAddress, namespaceBuffer, typeNameBuffer, timeout);

        return result.ReturnValue;
    }

    /// <summary>
    /// Encodes a managed string as a null-terminated UTF-8 buffer suitable for a native <c>const char*</c> argument.
    /// Embedded null characters are rejected because they would silently truncate the semantic identifier observed by the native API.
    /// </summary>
    /// <param name="value">The managed value to encode.</param>
    /// <param name="parameterName">The originating parameter name used when reporting invalid input.</param>
    /// <returns>A UTF-8 byte sequence containing exactly one trailing null terminator.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> contains an embedded null character.
    /// </exception>
    private static byte[] EncodeNullTerminatedUtf8(string value, string parameterName)
    {
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("Native string arguments cannot contain embedded null characters.", parameterName);

        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[] buffer = new byte[checked(byteCount + 1)];

        Encoding.UTF8.GetBytes(value, buffer);

        return buffer;
    }

    /// <summary>
    /// Retrieves the native methods declared by the specified IL2CPP class.
    /// Enumeration follows the iterator contract of <c>il2cpp_class_get_methods</c> while keeping iterator storage short-lived for every remote invocation.
    /// </summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose declared methods should be enumerated.</param>
    /// <param name="timeout">The maximum amount of time allowed for each individual runtime call.</param>
    /// <returns>An immutable snapshot containing the discovered native <c>MethodInfo*</c> addresses.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="classAddress"/> is zero.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the runtime iterator fails to progress or exceeds the defensive method-count limit.
    /// </exception>
    public IReadOnlyList<nint> GetMethods(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        List<nint> methods = new();
        nuint iterator = 0;

        while (methods.Count < MaximumMethodCount)
        {
            nuint previousIterator = iterator;
            RemoteCallPointerSizeOutResult result = _remoteCall.InvokePointerWithNuintRefArgument(_exports.ClassGetMethods, classAddress, iterator, timeout);
            iterator = result.OutValue;

            if (result.ReturnValue == 0)
                return methods.AsReadOnly();

            if (previousIterator != 0 && iterator == previousIterator)
                throw new InvalidDataException($"IL2CPP method enumeration for class 0x{classAddress:X} returned a method without advancing its iterator.");

            methods.Add(result.ReturnValue);
        }

        throw new InvalidDataException($"IL2CPP method enumeration for class 0x{classAddress:X} exceeded the defensive limit of {MaximumMethodCount} methods.");
    }

    /// <summary>
    /// Retrieves the semantic name associated with a runtime <c>MethodInfo</c>.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The managed method name exposed by IL2CPP.</returns>
    public string GetMethodName(nint methodAddress, TimeSpan timeout)
    {
        if (methodAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodAddress), "The IL2CPP method pointer cannot be zero.");

        RemoteCallResult result = _remoteCall.InvokePointer(_exports.MethodGetName, methodAddress, timeout);

        if (result.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null name pointer for method 0x{methodAddress:X}.");

        string name = _target.Memory.ReadNullTerminatedUtf8(result.ReturnValue, MaximumRuntimeNameLength);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"IL2CPP returned an empty name for method 0x{methodAddress:X}.");

        return name;
    }

    /// <summary>
    /// Retrieves the complete semantic signature of a runtime <c>MethodInfo</c>.
    /// Parameter and return types are resolved through the public IL2CPP type inspection API.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>A semantic runtime description of the requested method.</returns>
    public Il2CppMethodInfo GetMethodInfo(nint methodAddress, TimeSpan timeout)
    {
        string name = GetMethodName(methodAddress, timeout);
        uint parameterCount = _remoteCall.InvokeUInt32(_exports.MethodGetParamCount, methodAddress, timeout);

        if (parameterCount > MaximumMethodParameterCount)
            throw new InvalidDataException($"IL2CPP method 0x{methodAddress:X} reported an unreasonable parameter count of {parameterCount}.");

        RemoteCallResult returnTypeResult = _remoteCall.InvokePointer(_exports.MethodGetReturnType, methodAddress, timeout);

        if (returnTypeResult.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null return type for method 0x{methodAddress:X}.");

        string returnTypeName = GetTypeName(returnTypeResult.ReturnValue, timeout);
        List<string> parameterTypeNames = new(checked((int)parameterCount));

        for (uint index = 0; index < parameterCount; index++)
        {
            RemoteCallResult parameterResult = _remoteCall.InvokePointer(_exports.MethodGetParam, methodAddress, (nuint)index, timeout);

            if (parameterResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null parameter type for method 0x{methodAddress:X} at index {index}.");

            parameterTypeNames.Add(GetTypeName(parameterResult.ReturnValue, timeout));
        }

        return new Il2CppMethodInfo(methodAddress, name, returnTypeName, parameterTypeNames);
    }

    /// <summary>
    /// Retrieves the semantic UTF-8 name of an IL2CPP type and releases the temporary native string allocated by the runtime API.
    /// </summary>
    /// <param name="typeAddress">The native <c>Il2CppType*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each required native runtime call.</param>
    /// <returns>The semantic managed type name.</returns>
    private string GetTypeName(nint typeAddress, TimeSpan timeout)
    {
        if (typeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(typeAddress), "The IL2CPP type pointer cannot be zero.");

        RemoteCallResult result = _remoteCall.InvokePointer(_exports.TypeGetName, typeAddress, timeout);

        if (result.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null type-name pointer for type 0x{typeAddress:X}.");

        nint nameAddress = result.ReturnValue;
        string name;

        try
        {
            name = _target.Memory.ReadNullTerminatedUtf8(nameAddress, MaximumRuntimeNameLength);
        }
        catch
        {
            TryFreeRuntimeString(nameAddress, timeout);
            throw;
        }

        _remoteCall.InvokeVoid(_exports.Free, nameAddress, timeout);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"IL2CPP returned an empty semantic name for type 0x{typeAddress:X}.");

        return name;
    }

    /// <summary>
    /// Retrieves the native fields declared by the specified IL2CPP class.
    /// Enumeration follows the iterator contract exposed by <c>il2cpp_class_get_fields</c>.
    /// </summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose fields should be enumerated.</param>
    /// <param name="timeout">The maximum amount of time allowed for each individual runtime call.</param>
    /// <returns>An immutable snapshot containing the discovered native <c>FieldInfo*</c> addresses.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="classAddress"/> is zero.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the runtime iterator fails to progress or exceeds the defensive field-count limit.
    /// </exception>
    public IReadOnlyList<nint> GetFields(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        List<nint> fields = new();
        nuint iterator = 0;

        while (fields.Count < MaximumFieldCount)
        {
            nuint previousIterator = iterator;
            RemoteCallPointerSizeOutResult result = _remoteCall.InvokePointerWithNuintRefArgument(_exports.ClassGetFields, classAddress, iterator, timeout);
            iterator = result.OutValue;

            if (result.ReturnValue == 0)
                return fields.AsReadOnly();

            if (previousIterator != 0 && iterator == previousIterator)
                throw new InvalidDataException($"IL2CPP field enumeration for class 0x{classAddress:X} returned a field without advancing its iterator.");

            fields.Add(result.ReturnValue);
        }

        throw new InvalidDataException($"IL2CPP field enumeration for class 0x{classAddress:X} exceeded the defensive limit of {MaximumFieldCount} fields.");
    }

    /// <summary>
    /// Retrieves the semantic name associated with a runtime <c>FieldInfo</c>.
    /// </summary>
    /// <param name="fieldAddress">The native <c>FieldInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The managed field name exposed by IL2CPP.</returns>
    public string GetFieldName(nint fieldAddress, TimeSpan timeout)
    {
        if (fieldAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldAddress), "The IL2CPP field pointer cannot be zero.");

        RemoteCallResult result = _remoteCall.InvokePointer(_exports.FieldGetName, fieldAddress, timeout);

        if (result.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null name pointer for field 0x{fieldAddress:X}.");

        string name = _target.Memory.ReadNullTerminatedUtf8(result.ReturnValue, MaximumRuntimeNameLength);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"IL2CPP returned an empty name for field 0x{fieldAddress:X}.");

        return name;
    }

    /// <summary>
    /// Retrieves the complete semantic and storage description of a runtime <c>FieldInfo</c>.
    /// The operation resolves the field name, managed type, metadata attributes and raw IL2CPP storage offset without interpreting static storage as an absolute address.
    /// </summary>
    /// <param name="fieldAddress">The native <c>FieldInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>A semantic runtime description of the requested field.</returns>
    public Il2CppFieldInfo GetFieldInfo(nint fieldAddress, TimeSpan timeout)
    {
        if (fieldAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldAddress), "The IL2CPP field pointer cannot be zero.");

        string name = GetFieldName(fieldAddress, timeout);

        RemoteCallResult typeResult = _remoteCall.InvokePointer(_exports.FieldGetType, fieldAddress, timeout);

        if (typeResult.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null type for field 0x{fieldAddress:X}.");

        string typeName = GetTypeName(typeResult.ReturnValue, timeout);
        int rawAttributes = _remoteCall.InvokeInt32(_exports.FieldGetFlags, fieldAddress, timeout);
        nuint offset = _remoteCall.InvokeNuint(_exports.FieldGetOffset, fieldAddress, timeout);

        return new Il2CppFieldInfo(fieldAddress, name, typeName, (System.Reflection.FieldAttributes)rawAttributes, offset);
    }

    /// <summary>
    /// Attempts to release an IL2CPP-owned string while preserving an exception that has already occurred during string processing.
    /// Cleanup failure is intentionally suppressed only on this exceptional path so it cannot replace the original diagnostic.
    /// </summary>
    /// <param name="address">The IL2CPP-owned string address to release.</param>
    /// <param name="timeout">The maximum duration allowed for the cleanup call.</param>
    private void TryFreeRuntimeString(nint address, TimeSpan timeout)
    {
        try
        {
            _remoteCall.InvokeVoid(_exports.Free, address, timeout);
        }
        catch
        {
        }
    }
}