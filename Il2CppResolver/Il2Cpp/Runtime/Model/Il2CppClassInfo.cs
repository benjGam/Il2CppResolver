namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes one class discovered by enumerating an IL2CPP image through public runtime APIs.
/// The immutable runtime model carries only native class identity and the semantic namespace/name pair required to create public resolved types.
/// </summary>
internal sealed record Il2CppClassInfo
{
    /// <summary>Gets the native <c>Il2CppClass*</c> address.</summary>
    public nint ClassAddress { get; }
    /// <summary>Gets the managed namespace reported by IL2CPP. An empty string represents the global namespace.</summary>
    public string Namespace { get; }
    /// <summary>Gets the managed class name reported by IL2CPP.</summary>
    public string Name { get; }

    /// <summary>Initializes an immutable runtime class description.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> address.</param>
    /// <param name="namespaceName">The managed namespace reported by IL2CPP.</param>
    /// <param name="name">The managed class name reported by IL2CPP.</param>
    public Il2CppClassInfo(nint classAddress, string namespaceName, string name)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class address cannot be zero.");

        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ClassAddress = classAddress;
        Namespace = namespaceName;
        Name = name;
    }
}
