using System.Buffers.Binary;
using System.Text;
using UnityIl2CppResolver.Il2Cpp.Discovery;
using RuntimeAssemblyInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppAssemblyInfo;
using RuntimeClassInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassInfo;
using RuntimeClassMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppClassMetadata;
using RuntimeFieldInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppFieldInfo;
using RuntimeMethodInfo = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodInfo;
using RuntimeMethodMetadata = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppMethodMetadata;
using RuntimeTypeCode = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeCode;
using RuntimeTypeDescriptor = UnityIl2CppResolver.Il2Cpp.Runtime.Model.Il2CppTypeDescriptor;
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
    /// Defines the defensive maximum number of classes accepted while enumerating a single IL2CPP image.
    /// This value is intentionally generous while preventing corrupted runtime data from forcing unbounded class traversal.
    /// </summary>
    private const ulong MaximumImageClassCount = 1_000_000;

    /// <summary>
    /// Defines the defensive maximum number of methods accepted while enumerating a single IL2CPP class.
    /// </summary>
    private const int MaximumMethodCount = 65536;

    /// <summary>
    /// Defines the defensive maximum parameter count accepted for a single IL2CPP method.
    /// </summary>
    private const uint MaximumMethodParameterCount = 1024;

    /// <summary>
    /// Defines the maximum UTF-8 byte length accepted for runtime member and type names.
    /// </summary>
    private const int MaximumRuntimeNameLength = 4096;

    /// <summary>
    /// Defines the defensive maximum number of fields accepted while enumerating a single IL2CPP class.
    /// </summary>
    private const int MaximumFieldCount = 65536;

    /// <summary>
    /// Defines the defensive maximum number of properties accepted while enumerating a single IL2CPP class.
    /// </summary>
    private const int MaximumPropertyCount = 65536;

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
    /// Stores semantic type names already obtained through <c>il2cpp_type_get_name</c> for the current session snapshot.
    /// </summary>
    private readonly Dictionary<nint, string> _typeNames = new();

    /// <summary>
    /// Describes optional public IL2CPP runtime capabilities available on the current target.
    /// </summary>
    public Il2CppRuntimeCapabilities Capabilities { get; }

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
        Capabilities = new Il2CppRuntimeCapabilities(exports);
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
    public IReadOnlyList<RuntimeAssemblyInfo> GetAssemblyInfos(nint domain, TimeSpan timeout)
    {
        IReadOnlyList<nint> assemblies = GetAssemblies(domain, timeout);
        List<RuntimeAssemblyInfo> results = new(assemblies.Count);

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

            results.Add(new RuntimeAssemblyInfo(assemblyAddress, imageAddress, name));
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Retrieves every class exposed by an IL2CPP image through the optional public image/class enumeration APIs.
    /// The complete image is inspected only for explicit navigation operations; targeted semantic type resolution continues to use <c>il2cpp_class_from_name</c>.
    /// </summary>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> whose classes should be enumerated.</param>
    /// <param name="timeout">The maximum amount of time allowed for each individual native runtime call.</param>
    /// <returns>An immutable snapshot containing native class identity, namespace and name for every class exposed by the image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="imageAddress"/> is zero.</exception>
    /// <exception cref="NotSupportedException">Thrown when the target does not expose the required image/class enumeration exports.</exception>
    /// <exception cref="InvalidDataException">Thrown when IL2CPP reports an unreasonable class count or returns invalid class/name pointers.</exception>
    public IReadOnlyList<RuntimeClassInfo> GetClasses(nint imageAddress, TimeSpan timeout)
    {
        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image pointer cannot be zero.");

        if (!Capabilities.CanEnumerateImageTypes || _exports.ImageGetClassCount is null || _exports.ImageGetClass is null || _exports.ClassGetName is null || _exports.ClassGetNamespace is null)
            throw new NotSupportedException("The target does not expose the IL2CPP image/class APIs required for type enumeration.");

        nuint rawCount = _remoteCall.InvokeNuint(_exports.ImageGetClassCount.Value, imageAddress, timeout);
        ulong classCount = (ulong)rawCount;

        if (classCount == 0)
            return Array.Empty<RuntimeClassInfo>();

        if (classCount > MaximumImageClassCount || classCount > int.MaxValue)
            throw new InvalidDataException($"The IL2CPP runtime reported an unreasonable image class count of {classCount}.");

        List<RuntimeClassInfo> classes = new(checked((int)classCount));

        for (int index = 0; index < (int)classCount; index++)
        {
            RemoteCallResult classResult = _remoteCall.InvokePointer(_exports.ImageGetClass.Value, imageAddress, (nuint)index, timeout);

            if (classResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null class pointer for image 0x{imageAddress:X} at index {index}.");

            nint classAddress = classResult.ReturnValue;
            RemoteCallResult nameResult = _remoteCall.InvokePointer(_exports.ClassGetName.Value, classAddress, timeout);
            RemoteCallResult namespaceResult = _remoteCall.InvokePointer(_exports.ClassGetNamespace.Value, classAddress, timeout);

            if (nameResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null class-name pointer for class 0x{classAddress:X}.");

            if (namespaceResult.ReturnValue == 0)
                throw new InvalidDataException($"IL2CPP returned a null class-namespace pointer for class 0x{classAddress:X}.");

            string name = _target.Memory.ReadNullTerminatedUtf8(nameResult.ReturnValue, MaximumRuntimeNameLength);
            string namespaceName = _target.Memory.ReadNullTerminatedUtf8(namespaceResult.ReturnValue, MaximumRuntimeNameLength);

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException($"IL2CPP returned an empty class name for class 0x{classAddress:X}.");

            classes.Add(new RuntimeClassInfo(classAddress, imageAddress, namespaceName, name));
        }

        return classes.AsReadOnly();
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
    /// Retrieves the complete semantic signature of a runtime <c>MethodInfo</c> while reusing a method name already obtained during class-member indexing.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> to inspect.</param>
    /// <param name="knownName">The exact semantic method name already obtained from the runtime.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>A semantic runtime description of the requested method.</returns>
    public RuntimeMethodInfo GetMethodInfo(nint methodAddress, string knownName, TimeSpan timeout)
    {
        if (methodAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodAddress), "The IL2CPP method pointer cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(knownName);
        string name = knownName;
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

        return new RuntimeMethodInfo(methodAddress, name, returnTypeName, parameterTypeNames);
    }

    /// <summary>
    /// Retrieves the semantic UTF-8 name of an IL2CPP type and releases the temporary native string allocated by the runtime API.
    /// </summary>
    /// <param name="typeAddress">The native <c>Il2CppType*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each required native runtime call.</param>
    /// <returns>The semantic managed type name.</returns>
    public string GetTypeName(nint typeAddress, TimeSpan timeout)
    {
        if (typeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(typeAddress), "The IL2CPP type pointer cannot be zero.");

        if (_typeNames.TryGetValue(typeAddress, out string? cachedName))
            return cachedName;

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

        _typeNames[typeAddress] = name;
        return name;
    }

    /// <summary>
    /// Retrieves the native properties declared by the specified IL2CPP class through the optional property-enumeration runtime capability.
    /// Enumeration follows the iterator contract exposed by <c>il2cpp_class_get_properties</c>.
    /// </summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose properties should be enumerated.</param>
    /// <param name="timeout">The maximum amount of time allowed for each individual runtime call.</param>
    /// <returns>An immutable snapshot containing the discovered native <c>PropertyInfo*</c> addresses.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target does not expose the complete property navigation capability.</exception>
    public IReadOnlyList<nint> GetProperties(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        if (!_exports.ClassGetProperties.HasValue)
            throw new NotSupportedException("The target IL2CPP runtime does not expose property enumeration.");

        nint functionAddress = _exports.ClassGetProperties.Value;
        List<nint> properties = new();
        nuint iterator = 0;

        while (properties.Count < MaximumPropertyCount)
        {
            nuint previousIterator = iterator;
            RemoteCallPointerSizeOutResult result = _remoteCall.InvokePointerWithNuintRefArgument(functionAddress, classAddress, iterator, timeout);
            iterator = result.OutValue;

            if (result.ReturnValue == 0)
                return properties.AsReadOnly();

            if (previousIterator != 0 && iterator == previousIterator)
                throw new InvalidDataException($"IL2CPP property enumeration for class 0x{classAddress:X} returned a property without advancing its iterator.");

            properties.Add(result.ReturnValue);
        }

        throw new InvalidDataException($"IL2CPP property enumeration for class 0x{classAddress:X} exceeded the defensive limit of {MaximumPropertyCount} properties.");
    }

    /// <summary>Retrieves the semantic name associated with a runtime <c>PropertyInfo</c>.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The managed property name exposed by IL2CPP.</returns>
    public string GetPropertyName(nint propertyAddress, TimeSpan timeout)
    {
        if (propertyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyAddress), "The IL2CPP property pointer cannot be zero.");

        if (!_exports.PropertyGetName.HasValue)
            throw new NotSupportedException("The target IL2CPP runtime does not expose property-name inspection.");

        RemoteCallResult result = _remoteCall.InvokePointer(_exports.PropertyGetName.Value, propertyAddress, timeout);

        if (result.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null name pointer for property 0x{propertyAddress:X}.");

        string name = _target.Memory.ReadNullTerminatedUtf8(result.ReturnValue, MaximumRuntimeNameLength);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"IL2CPP returned an empty name for property 0x{propertyAddress:X}.");

        return name;
    }

    /// <summary>Retrieves the getter <c>MethodInfo*</c> associated with a runtime property.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The getter <c>MethodInfo*</c> address, or zero when the property has no getter.</returns>
    public nint GetPropertyGetterMethod(nint propertyAddress, TimeSpan timeout)
    {
        if (propertyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyAddress), "The IL2CPP property pointer cannot be zero.");

        if (!_exports.PropertyGetGetMethod.HasValue)
            throw new NotSupportedException("The target IL2CPP runtime does not expose property getter inspection.");

        return _remoteCall.InvokePointer(_exports.PropertyGetGetMethod.Value, propertyAddress, timeout).ReturnValue;
    }

    /// <summary>Retrieves the setter <c>MethodInfo*</c> associated with a runtime property.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The setter <c>MethodInfo*</c> address, or zero when the property has no setter.</returns>
    public nint GetPropertySetterMethod(nint propertyAddress, TimeSpan timeout)
    {
        if (propertyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyAddress), "The IL2CPP property pointer cannot be zero.");

        if (!_exports.PropertyGetSetMethod.HasValue)
            throw new NotSupportedException("The target IL2CPP runtime does not expose property setter inspection.");

        return _remoteCall.InvokePointer(_exports.PropertyGetSetMethod.Value, propertyAddress, timeout).ReturnValue;
    }

    /// <summary>Retrieves the metadata attributes associated with a runtime property.</summary>
    /// <param name="propertyAddress">The native <c>PropertyInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The managed property metadata attributes exposed by IL2CPP.</returns>
    public System.Reflection.PropertyAttributes GetPropertyAttributes(nint propertyAddress, TimeSpan timeout)
    {
        if (propertyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(propertyAddress), "The IL2CPP property pointer cannot be zero.");

        if (!_exports.PropertyGetFlags.HasValue)
            throw new NotSupportedException("The target IL2CPP runtime does not expose property-attribute inspection.");

        uint rawAttributes = _remoteCall.InvokeUInt32(_exports.PropertyGetFlags.Value, propertyAddress, timeout);
        return (System.Reflection.PropertyAttributes)rawAttributes;
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
    /// Retrieves the complete semantic and storage description of a runtime <c>FieldInfo</c> while reusing a field name already obtained during class-member indexing.
    /// </summary>
    /// <param name="fieldAddress">The native <c>FieldInfo*</c> to inspect.</param>
    /// <param name="knownName">The exact semantic field name already obtained from the runtime.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>A semantic runtime description of the requested field.</returns>
    public RuntimeFieldInfo GetFieldInfo(nint fieldAddress, string knownName, TimeSpan timeout)
    {
        if (fieldAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(fieldAddress), "The IL2CPP field pointer cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(knownName);
        string name = knownName;

        RemoteCallResult typeResult = _remoteCall.InvokePointer(_exports.FieldGetType, fieldAddress, timeout);

        if (typeResult.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null type for field 0x{fieldAddress:X}.");

        string typeName = GetTypeName(typeResult.ReturnValue, timeout);
        int rawAttributes = _remoteCall.InvokeInt32(_exports.FieldGetFlags, fieldAddress, timeout);
        nuint offset = _remoteCall.InvokeNuint(_exports.FieldGetOffset, fieldAddress, timeout);

        return new RuntimeFieldInfo(fieldAddress, name, typeResult.ReturnValue, typeName, (System.Reflection.FieldAttributes)rawAttributes, offset);
    }

    /// <summary>
    /// Retrieves the runtime type descriptor required to validate field-value interpretation.
    /// </summary>
    /// <param name="typeAddress">The native <c>Il2CppType*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>A runtime descriptor containing type category, class identity and enum information.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target does not expose the field-type inspection capability.</exception>
    public RuntimeTypeDescriptor GetTypeDescriptor(nint typeAddress, TimeSpan timeout)
    {
        if (typeAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(typeAddress), "The IL2CPP type pointer cannot be zero.");

        if (!Capabilities.CanInspectFieldValueTypes || _exports.TypeGetType is null || _exports.ClassFromType is null || _exports.ClassIsValueType is null || _exports.ClassIsEnum is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose the type APIs required for safe field-value interpretation.");

        RuntimeTypeCode typeCode = (RuntimeTypeCode)_remoteCall.InvokeInt32(_exports.TypeGetType.Value, typeAddress, timeout);
        string typeName = GetTypeName(typeAddress, timeout);
        nint classAddress = _remoteCall.InvokePointer(_exports.ClassFromType.Value, typeAddress, timeout).ReturnValue;
        bool isValueType = false;
        bool isEnum = false;
        RuntimeTypeCode? enumUnderlyingTypeCode = null;

        if (classAddress != 0)
        {
            isValueType = _remoteCall.InvokeBool(_exports.ClassIsValueType.Value, classAddress, timeout);
            isEnum = _remoteCall.InvokeBool(_exports.ClassIsEnum.Value, classAddress, timeout);

            if (isEnum)
            {
                if (_exports.ClassEnumBaseType is null)
                    throw new NotSupportedException("The target IL2CPP runtime does not expose enum underlying-type inspection.");

                nint enumBaseType = _remoteCall.InvokePointer(_exports.ClassEnumBaseType.Value, classAddress, timeout).ReturnValue;

                if (enumBaseType == 0)
                    throw new InvalidDataException($"IL2CPP returned a null enum base type for class 0x{classAddress:X}.");

                enumUnderlyingTypeCode = (RuntimeTypeCode)_remoteCall.InvokeInt32(_exports.TypeGetType.Value, enumBaseType, timeout);
            }
        }

        return new RuntimeTypeDescriptor(typeAddress, typeCode, typeName, classAddress, isValueType, isEnum, enumUnderlyingTypeCode);
    }

    /// <summary>
    /// Retrieves complete public metadata for one live IL2CPP class.
    /// </summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>The runtime class metadata snapshot.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target lacks the complete type-metadata capability.</exception>
    public RuntimeClassMetadata GetClassMetadata(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        if (!Capabilities.CanInspectTypeMetadata || _exports.ClassGetFlags is null || _exports.ClassGetTypeToken is null || _exports.ClassIsValueType is null || _exports.ClassIsEnum is null || _exports.ClassIsBlittable is null || _exports.ClassIsGeneric is null || _exports.ClassIsInflated is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose the class metadata inspection capability.");

        System.Reflection.TypeAttributes attributes = (System.Reflection.TypeAttributes)_remoteCall.InvokeInt32(_exports.ClassGetFlags.Value, classAddress, timeout);
        uint metadataToken = _remoteCall.InvokeUInt32(_exports.ClassGetTypeToken.Value, classAddress, timeout);
        bool isValueType = _remoteCall.InvokeBool(_exports.ClassIsValueType.Value, classAddress, timeout);
        bool isEnum = _remoteCall.InvokeBool(_exports.ClassIsEnum.Value, classAddress, timeout);
        bool isBlittable = _remoteCall.InvokeBool(_exports.ClassIsBlittable.Value, classAddress, timeout);
        bool isGeneric = _remoteCall.InvokeBool(_exports.ClassIsGeneric.Value, classAddress, timeout);
        bool isInflated = _remoteCall.InvokeBool(_exports.ClassIsInflated.Value, classAddress, timeout);
        int? valueSize = null;
        uint? valueAlignment = null;

        if (isValueType)
        {
            if (_exports.ClassValueSize is null)
                throw new NotSupportedException("The target IL2CPP runtime does not expose value-type size and alignment inspection.");

            RemoteCallUInt32OutResult sizeResult = _remoteCall.InvokeUInt32WithUInt32OutArgument(_exports.ClassValueSize.Value, classAddress, timeout);
            int size = unchecked((int)sizeResult.ReturnValue);

            if (size < 0)
                throw new InvalidDataException($"IL2CPP returned invalid negative value size {size} for class 0x{classAddress:X}.");

            valueSize = size;
            valueAlignment = sizeResult.OutValue;
        }

        return new RuntimeClassMetadata(classAddress, attributes, metadataToken, isValueType, isEnum, isBlittable, isGeneric, isInflated, valueSize, valueAlignment);
    }

    /// <summary>
    /// Retrieves complete public metadata for one live IL2CPP method.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <returns>The runtime method metadata snapshot.</returns>
    /// <exception cref="NotSupportedException">Thrown when the target lacks the complete method-metadata capability.</exception>
    public RuntimeMethodMetadata GetMethodMetadata(nint methodAddress, TimeSpan timeout)
    {
        if (methodAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodAddress), "The IL2CPP method pointer cannot be zero.");

        if (!Capabilities.CanInspectMethodMetadata || _exports.MethodGetFlags is null || _exports.MethodIsGeneric is null || _exports.MethodIsInflated is null || _exports.MethodGetToken is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose the complete method metadata inspection capability.");

        RemoteCallUInt32OutResult flagsResult = _remoteCall.InvokeUInt32WithUInt32OutArgument(_exports.MethodGetFlags.Value, methodAddress, timeout);
        System.Reflection.MethodAttributes attributes = (System.Reflection.MethodAttributes)flagsResult.ReturnValue;
        System.Reflection.MethodImplAttributes implementationAttributes = (System.Reflection.MethodImplAttributes)flagsResult.OutValue;
        uint metadataToken = _remoteCall.InvokeUInt32(_exports.MethodGetToken.Value, methodAddress, timeout);
        bool isGeneric = _remoteCall.InvokeBool(_exports.MethodIsGeneric.Value, methodAddress, timeout);
        bool isInflated = _remoteCall.InvokeBool(_exports.MethodIsInflated.Value, methodAddress, timeout);
        return new RuntimeMethodMetadata(methodAddress, attributes, implementationAttributes, metadataToken, isGeneric, isInflated);
    }

    /// <summary>Retrieves the total native instance size reported by IL2CPP for the specified class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The total instance size in bytes.</returns>
    public int GetClassInstanceSize(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        if (_exports.ClassInstanceSize is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose class instance-size inspection.");

        int size = _remoteCall.InvokeInt32(_exports.ClassInstanceSize.Value, classAddress, timeout);

        if (size <= 0)
            throw new InvalidDataException($"IL2CPP returned invalid instance size {size} for class 0x{classAddress:X}.");

        return size;
    }

    /// <summary>Tests whether the specified runtime class is a managed value type.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> to inspect.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns><see langword="true"/> when the class is a value type; otherwise <see langword="false"/>.</returns>
    public bool IsClassValueType(nint classAddress, TimeSpan timeout)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        if (_exports.ClassIsValueType is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose class value-type inspection.");

        return _remoteCall.InvokeBool(_exports.ClassIsValueType.Value, classAddress, timeout);
    }

    /// <summary>Retrieves the parent class of a runtime type.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose parent should be retrieved.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The parent <c>Il2CppClass*</c>, or zero when the type has no parent.</returns>
    public nint GetClassParent(nint classAddress, TimeSpan timeout)
    {
        ValidateClassAddress(classAddress);

        if (_exports.ClassGetParent is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose parent-type navigation.");

        return _remoteCall.InvokePointer(_exports.ClassGetParent.Value, classAddress, timeout).ReturnValue;
    }

    /// <summary>Retrieves the declaring type of a nested runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose declaring type should be retrieved.</param>
    /// <param name="timeout">The maximum duration allowed for the native runtime call.</param>
    /// <returns>The declaring <c>Il2CppClass*</c>, or zero for a top-level type.</returns>
    public nint GetClassDeclaringType(nint classAddress, TimeSpan timeout)
    {
        ValidateClassAddress(classAddress);

        if (_exports.ClassGetDeclaringType is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose declaring-type navigation.");

        return _remoteCall.InvokePointer(_exports.ClassGetDeclaringType.Value, classAddress, timeout).ReturnValue;
    }

    /// <summary>Enumerates every interface reported by IL2CPP for the specified runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose interfaces should be enumerated.</param>
    /// <param name="timeout">The maximum duration allowed for each iterator call.</param>
    /// <returns>An immutable list of interface <c>Il2CppClass*</c> addresses.</returns>
    public IReadOnlyList<nint> GetClassInterfaces(nint classAddress, TimeSpan timeout)
    {
        ValidateClassAddress(classAddress);

        if (_exports.ClassGetInterfaces is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose interface navigation.");

        return EnumerateClasses(_exports.ClassGetInterfaces.Value, classAddress, timeout, "interface");
    }

    /// <summary>Enumerates every nested class declared by the specified runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose nested types should be enumerated.</param>
    /// <param name="timeout">The maximum duration allowed for each iterator call.</param>
    /// <returns>An immutable list of nested <c>Il2CppClass*</c> addresses.</returns>
    public IReadOnlyList<nint> GetClassNestedTypes(nint classAddress, TimeSpan timeout)
    {
        ValidateClassAddress(classAddress);

        if (_exports.ClassGetNestedTypes is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose nested-type navigation.");

        return EnumerateClasses(_exports.ClassGetNestedTypes.Value, classAddress, timeout, "nested type");
    }

    /// <summary>Retrieves semantic identity and containing image for one runtime class.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> to describe.</param>
    /// <param name="timeout">The maximum duration allowed for each native runtime call.</param>
    /// <returns>The runtime class description used to materialize a public resolved type.</returns>
    public RuntimeClassInfo GetClassInfo(nint classAddress, TimeSpan timeout)
    {
        ValidateClassAddress(classAddress);

        if (_exports.ClassGetImage is null || _exports.ClassGetName is null || _exports.ClassGetNamespace is null)
            throw new NotSupportedException("The target IL2CPP runtime does not expose the class identity APIs required to materialize related runtime types.");

        nint imageAddress = _remoteCall.InvokePointer(_exports.ClassGetImage.Value, classAddress, timeout).ReturnValue;
        nint nameAddress = _remoteCall.InvokePointer(_exports.ClassGetName.Value, classAddress, timeout).ReturnValue;
        nint namespaceAddress = _remoteCall.InvokePointer(_exports.ClassGetNamespace.Value, classAddress, timeout).ReturnValue;

        if (imageAddress == 0)
            throw new InvalidDataException($"IL2CPP returned a null image for class 0x{classAddress:X}.");

        if (nameAddress == 0 || namespaceAddress == 0)
            throw new InvalidDataException($"IL2CPP returned an invalid semantic identity for class 0x{classAddress:X}.");

        string name = _target.Memory.ReadNullTerminatedUtf8(nameAddress, MaximumRuntimeNameLength);
        string namespaceName = _target.Memory.ReadNullTerminatedUtf8(namespaceAddress, MaximumRuntimeNameLength);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"IL2CPP returned an empty class name for class 0x{classAddress:X}.");

        return new RuntimeClassInfo(classAddress, imageAddress, namespaceName, name);
    }

    /// <summary>Enumerates classes through an IL2CPP iterator API receiving <c>Il2CppClass*</c> and <c>void**</c>.</summary>
    /// <param name="functionAddress">The iterator function address.</param>
    /// <param name="classAddress">The declaring class supplied as the first argument.</param>
    /// <param name="timeout">The maximum duration allowed for each iterator call.</param>
    /// <param name="entityName">The diagnostic entity name used in validation errors.</param>
    /// <returns>The immutable native class-address snapshot.</returns>
    private IReadOnlyList<nint> EnumerateClasses(nint functionAddress, nint classAddress, TimeSpan timeout, string entityName)
    {
        const int maximumRelatedTypeCount = 65536;
        List<nint> results = new();
        nuint iterator = 0;

        while (results.Count < maximumRelatedTypeCount)
        {
            nuint previousIterator = iterator;
            RemoteCallPointerSizeOutResult result = _remoteCall.InvokePointerWithNuintRefArgument(functionAddress, classAddress, iterator, timeout);
            iterator = result.OutValue;

            if (result.ReturnValue == 0)
                return results.AsReadOnly();

            if (previousIterator != 0 && iterator == previousIterator)
                throw new InvalidDataException($"IL2CPP {entityName} enumeration for class 0x{classAddress:X} returned an entry without advancing its iterator.");

            results.Add(result.ReturnValue);
        }

        throw new InvalidDataException($"IL2CPP {entityName} enumeration for class 0x{classAddress:X} exceeded the defensive limit of {maximumRelatedTypeCount} entries.");
    }

    /// <summary>Validates one native class identity before a class-oriented runtime operation.</summary>
    /// <param name="classAddress">The class address supplied by the caller.</param>
    private static void ValidateClassAddress(nint classAddress)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");
    }

    /// <summary>
    /// Attempts to retrieve the normal static-field data block of an IL2CPP class through optional public runtime APIs.
    /// The method returns <see langword="false"/> only when the target does not expose the complete capability; invalid runtime values still produce diagnostics.
    /// </summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> whose static storage should be inspected.</param>
    /// <param name="timeout">The maximum duration allowed for each individual runtime call.</param>
    /// <param name="staticFieldsAddress">Receives the native static-data block address when the capability is available.</param>
    /// <param name="staticFieldsSize">Receives the total static-data block size when the capability is available.</param>
    /// <returns><see langword="true"/> when the optional runtime APIs are available and produced a validated block; otherwise <see langword="false"/>.</returns>
    public bool TryGetStaticFieldStorage(nint classAddress, TimeSpan timeout, out nint staticFieldsAddress, out uint staticFieldsSize)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class pointer cannot be zero.");

        staticFieldsAddress = 0;
        staticFieldsSize = 0;

        if (!_exports.ClassGetStaticFieldData.HasValue || !_exports.ClassGetDataSize.HasValue)
            return false;

        nint dataFunction = _exports.ClassGetStaticFieldData.Value;
        nint sizeFunction = _exports.ClassGetDataSize.Value;
        RemoteCallResult dataResult = _remoteCall.InvokePointer(dataFunction, classAddress, timeout);
        uint dataSize = _remoteCall.InvokeUInt32(sizeFunction, classAddress, timeout);

        if (dataResult.ReturnValue == 0)
            throw new InvalidDataException($"IL2CPP returned a null static-field data pointer for class 0x{classAddress:X}.");

        if (dataSize == 0)
            throw new InvalidDataException($"IL2CPP returned a zero static-field data size for class 0x{classAddress:X}.");

        staticFieldsAddress = dataResult.ReturnValue;
        staticFieldsSize = dataSize;
        return true;
    }

    /// <summary>
    /// Invalidates local low-level runtime caches that are safe to rebuild from the live target.
    /// </summary>
    public void ClearCaches()
    {
        _typeNames.Clear();
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