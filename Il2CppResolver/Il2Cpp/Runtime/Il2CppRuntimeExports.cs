using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Provides a strongly typed representation of the native IL2CPP runtime exports consumed by the resolver.
/// Required exports must be present and directly callable, while optional exports are exposed as capabilities so higher-level resolution can prefer stable runtime APIs without rejecting older targets that lack them.
/// </summary>
internal sealed class Il2CppRuntimeExports
{
    /// <summary>Defines the export used to retrieve the active runtime domain.</summary>
    private const string DomainGetExportName = "il2cpp_domain_get";
    /// <summary>Defines the export used to enumerate assemblies loaded in a runtime domain.</summary>
    private const string DomainGetAssembliesExportName = "il2cpp_domain_get_assemblies";
    /// <summary>Defines the export used to retrieve the image associated with an assembly.</summary>
    private const string AssemblyGetImageExportName = "il2cpp_assembly_get_image";
    /// <summary>Defines the export used to retrieve the name associated with an image.</summary>
    private const string ImageGetNameExportName = "il2cpp_image_get_name";
    /// <summary>Defines the export used to resolve a class from an image, namespace and type name.</summary>
    private const string ClassFromNameExportName = "il2cpp_class_from_name";
    /// <summary>Defines the export used to enumerate methods declared by a class.</summary>
    private const string ClassGetMethodsExportName = "il2cpp_class_get_methods";
    /// <summary>Defines the export used to retrieve a method name.</summary>
    private const string MethodGetNameExportName = "il2cpp_method_get_name";
    /// <summary>Defines the export used to retrieve the number of parameters declared by a method.</summary>
    private const string MethodGetParamCountExportName = "il2cpp_method_get_param_count";
    /// <summary>Defines the export used to retrieve a method parameter type.</summary>
    private const string MethodGetParamExportName = "il2cpp_method_get_param";
    /// <summary>Defines the export used to retrieve a method return type.</summary>
    private const string MethodGetReturnTypeExportName = "il2cpp_method_get_return_type";
    /// <summary>Defines the export used to obtain the semantic name of an IL2CPP type.</summary>
    private const string TypeGetNameExportName = "il2cpp_type_get_name";
    /// <summary>Defines the export used to enumerate fields declared by a class.</summary>
    private const string ClassGetFieldsExportName = "il2cpp_class_get_fields";
    /// <summary>Defines the export used to retrieve the semantic name of a field.</summary>
    private const string FieldGetNameExportName = "il2cpp_field_get_name";
    /// <summary>Defines the export used to retrieve the managed type associated with a field.</summary>
    private const string FieldGetTypeExportName = "il2cpp_field_get_type";
    /// <summary>Defines the export used to retrieve metadata attributes associated with a field.</summary>
    private const string FieldGetFlagsExportName = "il2cpp_field_get_flags";
    /// <summary>Defines the export used to retrieve the runtime storage offset associated with a field.</summary>
    private const string FieldGetOffsetExportName = "il2cpp_field_get_offset";
    /// <summary>Defines the export used to release memory allocated by IL2CPP APIs.</summary>
    private const string FreeExportName = "il2cpp_free";
    /// <summary>Defines the optional export used to retrieve the number of classes exposed by an IL2CPP image.</summary>
    private const string ImageGetClassCountExportName = "il2cpp_image_get_class_count";
    /// <summary>Defines the optional export used to retrieve one class from an IL2CPP image by zero-based index.</summary>
    private const string ImageGetClassExportName = "il2cpp_image_get_class";
    /// <summary>Defines the optional export used to retrieve the managed name of an IL2CPP class.</summary>
    private const string ClassGetNameExportName = "il2cpp_class_get_name";
    /// <summary>Defines the optional export used to retrieve the managed namespace of an IL2CPP class.</summary>
    private const string ClassGetNamespaceExportName = "il2cpp_class_get_namespace";
    /// <summary>Defines the optional export used to retrieve the static-data pointer owned by an IL2CPP class.</summary>
    private const string ClassGetStaticFieldDataExportName = "il2cpp_class_get_static_field_data";
    /// <summary>Defines the optional export used to retrieve the static-data block size owned by an IL2CPP class.</summary>
    private const string ClassGetDataSizeExportName = "il2cpp_class_get_data_size";

    /// <summary>Gets the native address of <c>il2cpp_domain_get</c>.</summary>
    public nint DomainGet { get; }
    /// <summary>Gets the native address of <c>il2cpp_domain_get_assemblies</c>.</summary>
    public nint DomainGetAssemblies { get; }
    /// <summary>Gets the native address of <c>il2cpp_assembly_get_image</c>.</summary>
    public nint AssemblyGetImage { get; }
    /// <summary>Gets the native address of <c>il2cpp_image_get_name</c>.</summary>
    public nint ImageGetName { get; }
    /// <summary>Gets the native address of <c>il2cpp_class_from_name</c>.</summary>
    public nint ClassFromName { get; }
    /// <summary>Gets the native address of <c>il2cpp_class_get_methods</c>.</summary>
    public nint ClassGetMethods { get; }
    /// <summary>Gets the native address of <c>il2cpp_method_get_name</c>.</summary>
    public nint MethodGetName { get; }
    /// <summary>Gets the native address of <c>il2cpp_method_get_param_count</c>.</summary>
    public nint MethodGetParamCount { get; }
    /// <summary>Gets the native address of <c>il2cpp_method_get_param</c>.</summary>
    public nint MethodGetParam { get; }
    /// <summary>Gets the native address of <c>il2cpp_method_get_return_type</c>.</summary>
    public nint MethodGetReturnType { get; }
    /// <summary>Gets the native address of <c>il2cpp_type_get_name</c>.</summary>
    public nint TypeGetName { get; }
    /// <summary>Gets the native address of <c>il2cpp_class_get_fields</c>.</summary>
    public nint ClassGetFields { get; }
    /// <summary>Gets the native address of <c>il2cpp_field_get_name</c>.</summary>
    public nint FieldGetName { get; }
    /// <summary>Gets the native address of <c>il2cpp_field_get_type</c>.</summary>
    public nint FieldGetType { get; }
    /// <summary>Gets the native address of <c>il2cpp_field_get_flags</c>.</summary>
    public nint FieldGetFlags { get; }
    /// <summary>Gets the native address of <c>il2cpp_field_get_offset</c>.</summary>
    public nint FieldGetOffset { get; }
    /// <summary>Gets the native address of <c>il2cpp_free</c>.</summary>
    public nint Free { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_image_get_class_count</c>.</summary>
    public nint? ImageGetClassCount { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_image_get_class</c>.</summary>
    public nint? ImageGetClass { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_name</c>.</summary>
    public nint? ClassGetName { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_namespace</c>.</summary>
    public nint? ClassGetNamespace { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_static_field_data</c>.</summary>
    public nint? ClassGetStaticFieldData { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_data_size</c>.</summary>
    public nint? ClassGetDataSize { get; }

    /// <summary>
    /// Initializes a validated runtime export table.
    /// </summary>
    /// <param name="domainGet">The native address of <c>il2cpp_domain_get</c>.</param>
    /// <param name="domainGetAssemblies">The native address of <c>il2cpp_domain_get_assemblies</c>.</param>
    /// <param name="assemblyGetImage">The native address of <c>il2cpp_assembly_get_image</c>.</param>
    /// <param name="imageGetName">The native address of <c>il2cpp_image_get_name</c>.</param>
    /// <param name="classFromName">The native address of <c>il2cpp_class_from_name</c>.</param>
    /// <param name="classGetMethods">The native address of <c>il2cpp_class_get_methods</c>.</param>
    /// <param name="methodGetName">The native address of <c>il2cpp_method_get_name</c>.</param>
    /// <param name="methodGetParamCount">The native address of <c>il2cpp_method_get_param_count</c>.</param>
    /// <param name="methodGetParam">The native address of <c>il2cpp_method_get_param</c>.</param>
    /// <param name="methodGetReturnType">The native address of <c>il2cpp_method_get_return_type</c>.</param>
    /// <param name="classGetFields">The native address of <c>il2cpp_class_get_fields</c>.</param>
    /// <param name="fieldGetName">The native address of <c>il2cpp_field_get_name</c>.</param>
    /// <param name="fieldGetType">The native address of <c>il2cpp_field_get_type</c>.</param>
    /// <param name="fieldGetFlags">The native address of <c>il2cpp_field_get_flags</c>.</param>
    /// <param name="fieldGetOffset">The native address of <c>il2cpp_field_get_offset</c>.</param>
    /// <param name="typeGetName">The native address of <c>il2cpp_type_get_name</c>.</param>
    /// <param name="free">The native address of <c>il2cpp_free</c>.</param>
    /// <param name="imageGetClassCount">The optional native address of <c>il2cpp_image_get_class_count</c>.</param>
    /// <param name="imageGetClass">The optional native address of <c>il2cpp_image_get_class</c>.</param>
    /// <param name="classGetName">The optional native address of <c>il2cpp_class_get_name</c>.</param>
    /// <param name="classGetNamespace">The optional native address of <c>il2cpp_class_get_namespace</c>.</param>
    /// <param name="classGetStaticFieldData">The optional native address of <c>il2cpp_class_get_static_field_data</c>.</param>
    /// <param name="classGetDataSize">The optional native address of <c>il2cpp_class_get_data_size</c>.</param>
    private Il2CppRuntimeExports(nint domainGet, nint domainGetAssemblies, nint assemblyGetImage, nint imageGetName, nint classFromName, nint classGetMethods, nint methodGetName, nint methodGetParamCount, nint methodGetParam, nint methodGetReturnType, nint classGetFields, nint fieldGetName, nint fieldGetType, nint fieldGetFlags, nint fieldGetOffset, nint typeGetName, nint free, nint? imageGetClassCount, nint? imageGetClass, nint? classGetName, nint? classGetNamespace, nint? classGetStaticFieldData, nint? classGetDataSize)
    {
        DomainGet = domainGet;
        DomainGetAssemblies = domainGetAssemblies;
        AssemblyGetImage = assemblyGetImage;
        ImageGetName = imageGetName;
        ClassFromName = classFromName;
        ClassGetMethods = classGetMethods;
        MethodGetName = methodGetName;
        MethodGetParamCount = methodGetParamCount;
        MethodGetParam = methodGetParam;
        MethodGetReturnType = methodGetReturnType;
        ClassGetFields = classGetFields;
        FieldGetName = fieldGetName;
        FieldGetType = fieldGetType;
        FieldGetFlags = fieldGetFlags;
        FieldGetOffset = fieldGetOffset;
        TypeGetName = typeGetName;
        Free = free;
        ImageGetClassCount = imageGetClassCount;
        ImageGetClass = imageGetClass;
        ClassGetName = classGetName;
        ClassGetNamespace = classGetNamespace;
        ClassGetStaticFieldData = classGetStaticFieldData;
        ClassGetDataSize = classGetDataSize;
    }

    /// <summary>
    /// Resolves all required IL2CPP runtime entry points and discovers optional capabilities exposed by the target <c>GameAssembly.dll</c>.
    /// </summary>
    /// <param name="target">The validated IL2CPP target whose exports should be inspected.</param>
    /// <returns>A strongly typed runtime export table.</returns>
    /// <exception cref="InvalidDataException">Thrown when a required export is missing, forwarded or resolves outside the owning image.</exception>
    public static Il2CppRuntimeExports Resolve(Il2CppTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        PeImage image = target.GameAssemblyImage;

        return new Il2CppRuntimeExports(
            ResolveRequiredExport(image, DomainGetExportName),
            ResolveRequiredExport(image, DomainGetAssembliesExportName),
            ResolveRequiredExport(image, AssemblyGetImageExportName),
            ResolveRequiredExport(image, ImageGetNameExportName),
            ResolveRequiredExport(image, ClassFromNameExportName),
            ResolveRequiredExport(image, ClassGetMethodsExportName),
            ResolveRequiredExport(image, MethodGetNameExportName),
            ResolveRequiredExport(image, MethodGetParamCountExportName),
            ResolveRequiredExport(image, MethodGetParamExportName),
            ResolveRequiredExport(image, MethodGetReturnTypeExportName),
            ResolveRequiredExport(image, ClassGetFieldsExportName),
            ResolveRequiredExport(image, FieldGetNameExportName),
            ResolveRequiredExport(image, FieldGetTypeExportName),
            ResolveRequiredExport(image, FieldGetFlagsExportName),
            ResolveRequiredExport(image, FieldGetOffsetExportName),
            ResolveRequiredExport(image, TypeGetNameExportName),
            ResolveRequiredExport(image, FreeExportName),
            ResolveOptionalExport(image, ImageGetClassCountExportName),
            ResolveOptionalExport(image, ImageGetClassExportName),
            ResolveOptionalExport(image, ClassGetNameExportName),
            ResolveOptionalExport(image, ClassGetNamespaceExportName),
            ResolveOptionalExport(image, ClassGetStaticFieldDataExportName),
            ResolveOptionalExport(image, ClassGetDataSizeExportName));
    }

    /// <summary>
    /// Resolves a required direct IL2CPP export and validates that its address belongs to the loaded image.
    /// </summary>
    /// <param name="image">The parsed <c>GameAssembly.dll</c> image.</param>
    /// <param name="exportName">The exact export name to resolve.</param>
    /// <returns>The validated direct runtime address.</returns>
    private static nint ResolveRequiredExport(PeImage image, string exportName)
    {
        nint? address = ResolveOptionalExport(image, exportName);

        if (address is null)
            throw new InvalidDataException($"Required IL2CPP runtime export '{exportName}' was not found as a direct address inside GameAssembly.");

        return address.Value;
    }

    /// <summary>
    /// Resolves an optional direct IL2CPP export when present and valid.
    /// Missing or forwarded optional exports are treated as unavailable capabilities rather than target-detection failures.
    /// </summary>
    /// <param name="image">The parsed <c>GameAssembly.dll</c> image.</param>
    /// <param name="exportName">The exact export name to resolve.</param>
    /// <returns>The validated direct runtime address, or <see langword="null"/> when unavailable.</returns>
    private static nint? ResolveOptionalExport(PeImage image, string exportName)
    {
        PeExport? export = image.FindExport(exportName);

        if (export is null || export.IsForwarded || export.Address is null)
            return null;

        nint address = export.Address.Value;
        return image.ContainsAddress(address) ? address : null;
    }
}
