namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes one live IL2CPP class together with its containing image and semantic namespace/name identity.
/// The model is used by both image enumeration and relationship navigation so related classes can be materialized through the session identity map without assuming they belong to the originating assembly.
/// </summary>
internal sealed record Il2CppClassInfo
{
    /// <summary>Gets the native <c>Il2CppClass*</c> address.</summary>
    public nint ClassAddress { get; }
    /// <summary>Gets the native <c>Il2CppImage*</c> containing the class.</summary>
    public nint ImageAddress { get; }
    /// <summary>Gets the managed namespace reported by IL2CPP. An empty string represents the global namespace.</summary>
    public string Namespace { get; }
    /// <summary>Gets the managed class name reported by IL2CPP.</summary>
    public string Name { get; }

    /// <summary>Initializes an immutable runtime class description.</summary>
    /// <param name="classAddress">The native <c>Il2CppClass*</c> address.</param>
    /// <param name="imageAddress">The native <c>Il2CppImage*</c> containing the class.</param>
    /// <param name="namespaceName">The managed namespace reported by IL2CPP.</param>
    /// <param name="name">The managed class name reported by IL2CPP.</param>
    public Il2CppClassInfo(nint classAddress, nint imageAddress, string namespaceName, string name)
    {
        if (classAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(classAddress), "The IL2CPP class address cannot be zero.");

        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image address cannot be zero.");

        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ClassAddress = classAddress;
        ImageAddress = imageAddress;
        Namespace = namespaceName;
        Name = name;
    }
}
