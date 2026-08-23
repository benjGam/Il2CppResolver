using System.Reflection;

namespace UnityIl2CppResolver.Il2Cpp.Results;

/// <summary>
/// Represents cached public metadata for a resolved IL2CPP method.
/// The result is an immutable snapshot; reading its properties never triggers additional target-process work.
/// </summary>
public sealed class ResolvedMethodMetadata
{
    /// <summary>Gets the managed method attributes reported by IL2CPP.</summary>
    public MethodAttributes Attributes { get; }
    /// <summary>Gets the managed implementation attributes reported by IL2CPP.</summary>
    public MethodImplAttributes ImplementationAttributes { get; }
    /// <summary>Gets the metadata token associated with the method.</summary>
    public uint MetadataToken { get; }
    /// <summary>Gets a value indicating whether the method is static.</summary>
    public bool IsStatic => (Attributes & MethodAttributes.Static) != 0;
    /// <summary>Gets a value indicating whether the method requires an instance receiver.</summary>
    public bool IsInstance => !IsStatic;
    /// <summary>Gets a value indicating whether the method is public.</summary>
    public bool IsPublic => (Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    /// <summary>Gets a value indicating whether the method is virtual.</summary>
    public bool IsVirtual => (Attributes & MethodAttributes.Virtual) != 0;
    /// <summary>Gets a value indicating whether the method is abstract.</summary>
    public bool IsAbstract => (Attributes & MethodAttributes.Abstract) != 0;
    /// <summary>Gets a value indicating whether the method is final.</summary>
    public bool IsFinal => (Attributes & MethodAttributes.Final) != 0;
    /// <summary>Gets a value indicating whether the method is generic.</summary>
    public bool IsGeneric { get; }
    /// <summary>Gets a value indicating whether the method is an inflated generic instantiation.</summary>
    public bool IsInflated { get; }

    /// <summary>Initializes an immutable public method-metadata snapshot.</summary>
    /// <param name="attributes">The managed method attributes.</param>
    /// <param name="implementationAttributes">The method implementation attributes.</param>
    /// <param name="metadataToken">The metadata token associated with the method.</param>
    /// <param name="isGeneric">Whether the method is generic.</param>
    /// <param name="isInflated">Whether the method is an inflated generic instantiation.</param>
    internal ResolvedMethodMetadata(MethodAttributes attributes, MethodImplAttributes implementationAttributes, uint metadataToken, bool isGeneric, bool isInflated)
    {
        Attributes = attributes;
        ImplementationAttributes = implementationAttributes;
        MetadataToken = metadataToken;
        IsGeneric = isGeneric;
        IsInflated = isInflated;
    }
}
