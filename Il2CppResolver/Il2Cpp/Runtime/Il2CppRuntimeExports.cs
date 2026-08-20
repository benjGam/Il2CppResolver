using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Provides a strongly typed and validated representation of the native IL2CPP runtime entry points required by the resolver.
/// This class belongs to the IL2CPP runtime layer and converts generic PE exports exposed by <c>GameAssembly.dll</c> into explicit native function addresses.
/// Higher-level runtime components depend on this class instead of performing PE export lookups or manipulating export names directly.
/// </summary>
internal sealed class Il2CppRuntimeExports
{
    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the active runtime domain.
    /// </summary>
    private const string DomainGetExportName = "il2cpp_domain_get";

    /// <summary>
    /// Defines the exported IL2CPP function used to enumerate assemblies loaded in a runtime domain.
    /// </summary>
    private const string DomainGetAssembliesExportName = "il2cpp_domain_get_assemblies";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the image associated with an assembly.
    /// </summary>
    private const string AssemblyGetImageExportName = "il2cpp_assembly_get_image";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the name associated with an image.
    /// </summary>
    private const string ImageGetNameExportName = "il2cpp_image_get_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to resolve a class from an image, namespace and type name.
    /// </summary>
    private const string ClassFromNameExportName = "il2cpp_class_from_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve a method from a class by name and argument count.
    /// </summary>
    private const string ClassGetMethodFromNameExportName = "il2cpp_class_get_method_from_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to enumerate methods declared by a class.
    /// </summary>
    private const string ClassGetMethodsExportName = "il2cpp_class_get_methods";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve a method name.
    /// </summary>
    private const string MethodGetNameExportName = "il2cpp_method_get_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the number of parameters declared by a method.
    /// </summary>
    private const string MethodGetParamCountExportName = "il2cpp_method_get_param_count";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve a method parameter type.
    /// </summary>
    private const string MethodGetParamExportName = "il2cpp_method_get_param";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve a method return type.
    /// </summary>
    private const string MethodGetReturnTypeExportName = "il2cpp_method_get_return_type";

    /// <summary>
    /// Defines the exported IL2CPP function used to obtain the semantic name of an IL2CPP type.
    /// </summary>
    private const string TypeGetNameExportName = "il2cpp_type_get_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to enumerate fields declared by a class.
    /// </summary>
    private const string ClassGetFieldsExportName = "il2cpp_class_get_fields";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the semantic name of a field.
    /// </summary>
    private const string FieldGetNameExportName = "il2cpp_field_get_name";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the managed type associated with a field.
    /// </summary>
    private const string FieldGetTypeExportName = "il2cpp_field_get_type";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the metadata attributes associated with a field.
    /// </summary>
    private const string FieldGetFlagsExportName = "il2cpp_field_get_flags";

    /// <summary>
    /// Defines the exported IL2CPP function used to retrieve the runtime storage offset associated with a field.
    /// </summary>
    private const string FieldGetOffsetExportName = "il2cpp_field_get_offset";

    /// <summary>
    /// Defines the exported IL2CPP function used to release memory allocated by IL2CPP APIs.
    /// </summary>
    private const string FreeExportName = "il2cpp_free";


    /// <summary>
    /// Gets the native address of <c>il2cpp_domain_get</c>.
    /// This entry point is the root of runtime domain discovery and will be used by the runtime backend to obtain the active <c>Il2CppDomain</c>.
    /// </summary>
    public nint DomainGet { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_domain_get_assemblies</c>.
    /// This entry point allows the runtime backend to enumerate all assemblies currently loaded by the IL2CPP domain.
    /// </summary>
    public nint DomainGetAssemblies { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_assembly_get_image</c>.
    /// This entry point converts an <c>Il2CppAssembly</c> reference into its associated <c>Il2CppImage</c>.
    /// </summary>
    public nint AssemblyGetImage { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_image_get_name</c>.
    /// This entry point exposes the managed assembly image name used by semantic assembly resolution.
    /// </summary>
    public nint ImageGetName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_class_from_name</c>.
    /// This entry point resolves an <c>Il2CppClass</c> from an image, namespace and type name.
    /// </summary>
    public nint ClassFromName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_class_get_method_from_name</c>.
    /// This entry point provides basic method lookup by method name and argument count within an IL2CPP class.
    /// More precise overload resolution will later rely on additional runtime inspection primitives.
    /// </summary>
    public nint ClassGetMethodFromName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_class_get_methods</c>.
    /// </summary>
    public nint ClassGetMethods { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_method_get_name</c>.
    /// </summary>
    public nint MethodGetName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_method_get_param_count</c>.
    /// </summary>
    public nint MethodGetParamCount { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_method_get_param</c>.
    /// </summary>
    public nint MethodGetParam { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_method_get_return_type</c>.
    /// </summary>
    public nint MethodGetReturnType { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_type_get_name</c>.
    /// Returned strings are owned by IL2CPP and must be released through <see cref="Free"/> after their contents have been copied.
    /// </summary>
    public nint TypeGetName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_class_get_fields</c>.
    /// </summary>
    public nint ClassGetFields { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_field_get_name</c>.
    /// </summary>
    public nint FieldGetName { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_field_get_type</c>.
    /// </summary>
    public nint FieldGetType { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_field_get_flags</c>.
    /// </summary>
    public nint FieldGetFlags { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_field_get_offset</c>.
    /// </summary>
    public nint FieldGetOffset { get; }

    /// <summary>
    /// Gets the native address of <c>il2cpp_free</c>.
    /// </summary>
    public nint Free { get; }

    /// <summary>
    /// Initializes a validated table of native IL2CPP runtime entry points.
    /// Instances are created exclusively through <see cref="Resolve(Il2CppTarget)"/> after every required PE export has been validated as a direct native address.
    /// </summary>
    /// <param name="domainGet">The native address of <c>il2cpp_domain_get</c>.</param>
    /// <param name="domainGetAssemblies">The native address of <c>il2cpp_domain_get_assemblies</c>.</param>
    /// <param name="assemblyGetImage">The native address of <c>il2cpp_assembly_get_image</c>.</param>
    /// <param name="imageGetName">The native address of <c>il2cpp_image_get_name</c>.</param>
    /// <param name="classFromName">The native address of <c>il2cpp_class_from_name</c>.</param>
    /// <param name="classGetMethodFromName">The native address of <c>il2cpp_class_get_method_from_name</c>.</param>
    private Il2CppRuntimeExports(
        nint domainGet,
        nint domainGetAssemblies,
        nint assemblyGetImage,
        nint imageGetName,
        nint classFromName,
        nint classGetMethodFromName,
        nint classGetMethods,
        nint methodGetName,
        nint methodGetParamCount,
        nint methodGetParam,
        nint methodGetReturnType,
        nint classGetFields,
        nint fieldGetName,
        nint fieldGetType,
        nint fieldGetFlags,
        nint fieldGetOffset,
        nint typeGetName,
        nint free)
    {
        DomainGet = domainGet;
        DomainGetAssemblies = domainGetAssemblies;
        AssemblyGetImage = assemblyGetImage;
        ImageGetName = imageGetName;
        ClassFromName = classFromName;
        ClassGetMethodFromName = classGetMethodFromName;
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
    }

    /// <summary>
    /// Resolves and validates the native IL2CPP runtime entry points required by the initial runtime resolution backend.
    /// Every required symbol must exist as a direct PE export inside the target <c>GameAssembly.dll</c>; forwarded or missing exports cause the operation to fail immediately.
    /// </summary>
    /// <param name="target">The validated IL2CPP target whose <c>GameAssembly.dll</c> exports should be resolved.</param>
    /// <returns>A strongly typed table containing the validated native addresses of the required IL2CPP runtime functions.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when a required IL2CPP export is missing, forwarded or does not resolve to a valid address inside <c>GameAssembly.dll</c>.
    /// </exception>
    public static Il2CppRuntimeExports Resolve(Il2CppTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        PeImage image = target.GameAssemblyImage;

        nint domainGet = ResolveRequiredExport(image, DomainGetExportName);
        nint domainGetAssemblies = ResolveRequiredExport(image, DomainGetAssembliesExportName);
        nint assemblyGetImage = ResolveRequiredExport(image, AssemblyGetImageExportName);
        nint imageGetName = ResolveRequiredExport(image, ImageGetNameExportName);
        nint classFromName = ResolveRequiredExport(image, ClassFromNameExportName);
        nint classGetMethodFromName = ResolveRequiredExport(image, ClassGetMethodFromNameExportName);
        nint classGetMethods = ResolveRequiredExport(image, ClassGetMethodsExportName);
        nint methodGetName = ResolveRequiredExport(image, MethodGetNameExportName);
        nint methodGetParamCount = ResolveRequiredExport(image, MethodGetParamCountExportName);
        nint methodGetParam = ResolveRequiredExport(image, MethodGetParamExportName);
        nint methodGetReturnType = ResolveRequiredExport(image, MethodGetReturnTypeExportName);
        nint typeGetName = ResolveRequiredExport(image, TypeGetNameExportName);
        nint classGetFields = ResolveRequiredExport(image, ClassGetFieldsExportName);
        nint fieldGetName = ResolveRequiredExport(image, FieldGetNameExportName);
        nint fieldGetType = ResolveRequiredExport(image, FieldGetTypeExportName);
        nint fieldGetFlags = ResolveRequiredExport(image, FieldGetFlagsExportName);
        nint fieldGetOffset = ResolveRequiredExport(image, FieldGetOffsetExportName);
        nint free = ResolveRequiredExport(image, FreeExportName);

        return new Il2CppRuntimeExports(
            domainGet,
            domainGetAssemblies,
            assemblyGetImage,
            imageGetName,
            classFromName,
            classGetMethodFromName,
            classGetMethods,
            methodGetName,
            methodGetParamCount,
            methodGetParam,
            methodGetReturnType,
            classGetFields,
            fieldGetName,
            fieldGetType,
            fieldGetFlags,
            fieldGetOffset,
            typeGetName,
            free);
    }

    /// <summary>
    /// Resolves a required IL2CPP runtime function from the PE export table and validates that it represents a direct address inside the loaded image.
    /// This method centralizes the native export validation rules so higher-level runtime components can operate exclusively on trusted function addresses.
    /// </summary>
    /// <param name="image">The parsed <c>GameAssembly.dll</c> PE image containing the IL2CPP exports.</param>
    /// <param name="exportName">The exact public name of the required IL2CPP runtime export.</param>
    /// <returns>The validated absolute runtime address of the exported function.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the export is missing, forwarded, has no direct runtime address or resolves outside the owning PE image.
    /// </exception>
    private static nint ResolveRequiredExport(PeImage image, string exportName)
    {
        PeExport? export = image.FindExport(exportName);

        if (export is null)
            throw new InvalidDataException($"Required IL2CPP runtime export '{exportName}' was not found in GameAssembly.");

        if (export.IsForwarded || export.Address is null)
            throw new InvalidDataException($"IL2CPP runtime export '{exportName}' does not resolve to a direct native address.");

        nint address = export.Address.Value;

        if (!image.ContainsAddress(address))
            throw new InvalidDataException($"IL2CPP runtime export '{exportName}' resolves outside the GameAssembly image.");

        return address;
    }
}