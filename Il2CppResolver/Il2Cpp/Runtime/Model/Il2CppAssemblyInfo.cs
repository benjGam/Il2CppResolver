namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Describes an assembly discovered through the live IL2CPP runtime of the target process.
/// This immutable model belongs to the runtime introspection layer and associates the native <c>Il2CppAssembly</c> and <c>Il2CppImage</c> addresses with the semantic image name exposed by IL2CPP.
/// Higher-level resolution components can use this representation to identify assemblies without depending on raw runtime pointers or metadata files.
/// </summary>
internal sealed record Il2CppAssemblyInfo
{
    /// <summary>
    /// Gets the native address of the <c>Il2CppAssembly</c> instance registered in the active IL2CPP domain.
    /// </summary>
    public nint AssemblyAddress { get; }

    /// <summary>
    /// Gets the native address of the <c>Il2CppImage</c> associated with this assembly.
    /// The image represents the semantic assembly contents used by IL2CPP class and type resolution APIs.
    /// </summary>
    public nint ImageAddress { get; }

    /// <summary>
    /// Gets the image name exposed by <c>il2cpp_image_get_name</c>.
    /// Typical values include <c>Assembly-CSharp.dll</c>, <c>UnityEngine.CoreModule.dll</c> and <c>Unity.InputSystem.dll</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes an immutable description of an assembly discovered through the live IL2CPP runtime.
    /// </summary>
    /// <param name="assemblyAddress">The native address of the corresponding <c>Il2CppAssembly</c> instance.</param>
    /// <param name="imageAddress">The native address of the corresponding <c>Il2CppImage</c> instance.</param>
    /// <param name="name">The semantic image name reported by the IL2CPP runtime.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="assemblyAddress"/> or <paramref name="imageAddress"/> is zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    internal Il2CppAssemblyInfo(nint assemblyAddress, nint imageAddress, string name)
    {
        if (assemblyAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(assemblyAddress), "The IL2CPP assembly address cannot be zero.");

        if (imageAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(imageAddress), "The IL2CPP image address cannot be zero.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        AssemblyAddress = assemblyAddress;
        ImageAddress = imageAddress;
        Name = name;
    }
}