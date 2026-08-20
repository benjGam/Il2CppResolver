namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes a method discovered through the live IL2CPP runtime.
/// This immutable runtime model associates a native <c>MethodInfo*</c> with its semantic method name, return type and ordered parameter type names.
/// It deliberately does not expose a compiled native code address because resolving executable method pointers is a separate compatibility concern.
/// </summary>
internal sealed record Il2CppMethodInfo
{
    /// <summary>
    /// Gets the native address of the runtime <c>MethodInfo</c> structure representing this method.
    /// </summary>
    public nint MethodAddress { get; }

    /// <summary>
    /// Gets the semantic managed method name exposed by the IL2CPP runtime.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the semantic name of the method return type.
    /// </summary>
    public string ReturnTypeName { get; }

    /// <summary>
    /// Gets the ordered semantic names of the method parameter types.
    /// </summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>
    /// Gets the number of parameters declared by the method.
    /// </summary>
    public int ParameterCount => ParameterTypeNames.Count;

    /// <summary>
    /// Initializes an immutable runtime method description.
    /// </summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> address.</param>
    /// <param name="name">The semantic method name.</param>
    /// <param name="returnTypeName">The semantic return type name.</param>
    /// <param name="parameterTypeNames">The ordered semantic parameter type names.</param>
    internal Il2CppMethodInfo(nint methodAddress, string name, string returnTypeName, IReadOnlyList<string> parameterTypeNames)
    {
        if (methodAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodAddress), "The IL2CPP method address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnTypeName);
        ArgumentNullException.ThrowIfNull(parameterTypeNames);

        string[] parameters = parameterTypeNames.ToArray();

        if (parameters.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Method parameter type names cannot contain empty values.", nameof(parameterTypeNames));

        MethodAddress = methodAddress;
        Name = name;
        ReturnTypeName = returnTypeName;
        ParameterTypeNames = Array.AsReadOnly(parameters);
    }
}