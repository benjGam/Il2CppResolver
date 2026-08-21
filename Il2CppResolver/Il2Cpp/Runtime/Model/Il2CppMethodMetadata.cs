using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Runtime.Model;

/// <summary>
/// Stores runtime metadata obtained for one live IL2CPP <c>MethodInfo*</c> through public method-inspection APIs.
/// </summary>
internal sealed class Il2CppMethodMetadata
{
    /// <summary>Gets the native method identity described by this metadata.</summary>
    public nint MethodAddress { get; }
    /// <summary>Gets the managed method attributes reported by IL2CPP.</summary>
    public MethodAttributes Attributes { get; }
    /// <summary>Gets the managed implementation attributes reported through the method-flags output parameter.</summary>
    public MethodImplAttributes ImplementationAttributes { get; }
    /// <summary>Gets the metadata token associated with the method.</summary>
    public uint MetadataToken { get; }
    /// <summary>Gets a value indicating whether the method is generic.</summary>
    public bool IsGeneric { get; }
    /// <summary>Gets a value indicating whether the method represents an inflated generic instantiation.</summary>
    public bool IsInflated { get; }

    /// <summary>Initializes immutable runtime method metadata.</summary>
    /// <param name="methodAddress">The native <c>MethodInfo*</c> address.</param>
    /// <param name="attributes">The managed method attributes.</param>
    /// <param name="implementationAttributes">The managed implementation attributes.</param>
    /// <param name="metadataToken">The method metadata token.</param>
    /// <param name="isGeneric">Whether the method is generic.</param>
    /// <param name="isInflated">Whether the method is an inflated generic instantiation.</param>
    public Il2CppMethodMetadata(nint methodAddress, MethodAttributes attributes, MethodImplAttributes implementationAttributes, uint metadataToken, bool isGeneric, bool isInflated)
    {
        if (methodAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(methodAddress), "The IL2CPP method address cannot be zero.");

        MethodAddress = methodAddress;
        Attributes = attributes;
        ImplementationAttributes = implementationAttributes;
        MetadataToken = metadataToken;
        IsGeneric = isGeneric;
        IsInflated = isInflated;
    }
}
