using UnityIl2CppResolver.Il2Cpp.Discovery;
using UnityIl2CppResolver.Native.PE;

namespace UnityIl2CppResolver.Il2Cpp.Runtime;

/// <summary>
/// Provides a strongly typed representation of the native IL2CPP runtime exports consumed by the resolver.
/// Required exports must be present and directly callable, while optional exports expose capability-driven navigation, metadata and value-reading features without making process attachment unnecessarily strict.
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

    /// <summary>Defines the optional export used to enumerate properties declared by a class.</summary>
    private const string ClassGetPropertiesExportName = "il2cpp_class_get_properties";
    /// <summary>Defines the optional export used to retrieve the semantic name of a property.</summary>
    private const string PropertyGetNameExportName = "il2cpp_property_get_name";
    /// <summary>Defines the optional export used to retrieve property metadata attributes.</summary>
    private const string PropertyGetFlagsExportName = "il2cpp_property_get_flags";
    /// <summary>Defines the optional export used to retrieve the getter method associated with a property.</summary>
    private const string PropertyGetGetMethodExportName = "il2cpp_property_get_get_method";
    /// <summary>Defines the optional export used to retrieve the setter method associated with a property.</summary>
    private const string PropertyGetSetMethodExportName = "il2cpp_property_get_set_method";
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

    /// <summary>Defines the optional export used to retrieve the native IL2CPP type category.</summary>
    private const string TypeGetTypeExportName = "il2cpp_type_get_type";
    /// <summary>Defines the modern optional export used to map an <c>Il2CppType*</c> to its runtime class.</summary>
    private const string ClassFromTypeExportName = "il2cpp_class_from_type";
    /// <summary>Defines the legacy-compatible optional export name used to map an <c>Il2CppType*</c> to its runtime class.</summary>
    private const string ClassFromIl2CppTypeExportName = "il2cpp_class_from_il2cpp_type";
    /// <summary>Defines the optional export used to retrieve the underlying type of an enum class.</summary>
    private const string ClassEnumBaseTypeExportName = "il2cpp_class_enum_basetype";
    /// <summary>Defines the optional export used to test whether a class is a value type.</summary>
    private const string ClassIsValueTypeExportName = "il2cpp_class_is_valuetype";
    /// <summary>Defines the optional export used to test whether a class is an enum.</summary>
    private const string ClassIsEnumExportName = "il2cpp_class_is_enum";
    /// <summary>Defines the optional export used to test whether a class is blittable.</summary>
    private const string ClassIsBlittableExportName = "il2cpp_class_is_blittable";
    /// <summary>Defines the optional export used to test whether a class is generic.</summary>
    private const string ClassIsGenericExportName = "il2cpp_class_is_generic";
    /// <summary>Defines the optional export used to test whether a class is an inflated generic instantiation.</summary>
    private const string ClassIsInflatedExportName = "il2cpp_class_is_inflated";
    /// <summary>Defines the optional export used to retrieve managed type attributes from a class.</summary>
    private const string ClassGetFlagsExportName = "il2cpp_class_get_flags";
    /// <summary>Defines the optional export used to retrieve value-type size and alignment.</summary>
    private const string ClassValueSizeExportName = "il2cpp_class_value_size";
    /// <summary>Defines the optional export used to retrieve the metadata token associated with a class.</summary>
    private const string ClassGetTypeTokenExportName = "il2cpp_class_get_type_token";
    /// <summary>Defines the optional export used to retrieve the total instance size associated with a class.</summary>
    private const string ClassInstanceSizeExportName = "il2cpp_class_instance_size";
    /// <summary>Defines the optional export used to retrieve the parent class.</summary>
    private const string ClassGetParentExportName = "il2cpp_class_get_parent";
    /// <summary>Defines the optional export used to enumerate implemented interfaces.</summary>
    private const string ClassGetInterfacesExportName = "il2cpp_class_get_interfaces";
    /// <summary>Defines the optional export used to enumerate nested types.</summary>
    private const string ClassGetNestedTypesExportName = "il2cpp_class_get_nested_types";
    /// <summary>Defines the optional export used to retrieve a nested class's declaring type.</summary>
    private const string ClassGetDeclaringTypeExportName = "il2cpp_class_get_declaring_type";
    /// <summary>Defines the optional export used to retrieve the image containing a class.</summary>
    private const string ClassGetImageExportName = "il2cpp_class_get_image";
    /// <summary>Defines the optional export used to retrieve method and implementation flags.</summary>
    private const string MethodGetFlagsExportName = "il2cpp_method_get_flags";
    /// <summary>Defines the optional export used to test whether a method is generic.</summary>
    private const string MethodIsGenericExportName = "il2cpp_method_is_generic";
    /// <summary>Defines the optional export used to test whether a method is an inflated generic instantiation.</summary>
    private const string MethodIsInflatedExportName = "il2cpp_method_is_inflated";
    /// <summary>Defines the optional export used to retrieve the metadata token associated with a method.</summary>
    private const string MethodGetTokenExportName = "il2cpp_method_get_token";

    /// <summary>Defines the optional export used to retrieve a managed string length.</summary>
    private const string StringLengthExportName = "il2cpp_string_length";
    /// <summary>Defines the optional export used to retrieve the UTF-16 character buffer of a managed string.</summary>
    private const string StringCharsExportName = "il2cpp_string_chars";
    /// <summary>Defines the optional export used to retrieve the logical length of a managed array.</summary>
    private const string ArrayLengthExportName = "il2cpp_array_length";
    /// <summary>Defines the optional export used to retrieve the payload byte length of a managed array.</summary>
    private const string ArrayGetByteLengthExportName = "il2cpp_array_get_byte_length";
    /// <summary>Defines the optional export used to retrieve the native element size of an array class.</summary>
    private const string ArrayElementSizeExportName = "il2cpp_array_element_size";
    /// <summary>Defines the optional export used to retrieve the element class represented by an array class.</summary>
    private const string ClassGetElementClassExportName = "il2cpp_class_get_element_class";
    /// <summary>Defines the optional export used to retrieve the canonical IL2CPP type represented by a class.</summary>
    private const string ClassGetTypeExportName = "il2cpp_class_get_type";

    /// <summary>Defines the optional export used to invoke one managed method through the supported IL2CPP embedding API.</summary>
    private const string RuntimeInvokeExportName = "il2cpp_runtime_invoke";
    /// <summary>Defines the optional export used to retrieve the raw payload address from a boxed managed value.</summary>
    private const string ObjectUnboxExportName = "il2cpp_object_unbox";
    /// <summary>Defines the optional export used to attach the current native thread to an IL2CPP domain.</summary>
    private const string ThreadAttachExportName = "il2cpp_thread_attach";
    /// <summary>Defines the optional export used to detach one previously attached native thread from IL2CPP.</summary>
    private const string ThreadDetachExportName = "il2cpp_thread_detach";
    /// <summary>Defines the optional export used to determine whether a method requires a managed instance.</summary>
    private const string MethodIsInstanceExportName = "il2cpp_method_is_instance";
    /// <summary>Defines the optional export used to retrieve the runtime class of a managed object.</summary>
    private const string ObjectGetClassExportName = "il2cpp_object_get_class";
    /// <summary>Defines the optional export used to resolve a virtual method against a concrete managed object.</summary>
    private const string ObjectGetVirtualMethodExportName = "il2cpp_object_get_virtual_method";
    /// <summary>Defines the optional export used to validate that an object class can be assigned to a declaring class.</summary>
    private const string ClassIsAssignableFromExportName = "il2cpp_class_is_assignable_from";
    /// <summary>Defines the optional export used to create a strong GC handle for one managed object.</summary>
    private const string GcHandleNewExportName = "il2cpp_gchandle_new";
    /// <summary>Defines the optional export used to release one previously created GC handle.</summary>
    private const string GcHandleFreeExportName = "il2cpp_gchandle_free";

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

    /// <summary>Gets the optional native address of <c>il2cpp_class_get_properties</c>.</summary>
    public nint? ClassGetProperties { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_property_get_name</c>.</summary>
    public nint? PropertyGetName { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_property_get_flags</c>.</summary>
    public nint? PropertyGetFlags { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_property_get_get_method</c>.</summary>
    public nint? PropertyGetGetMethod { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_property_get_set_method</c>.</summary>
    public nint? PropertyGetSetMethod { get; }
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
    /// <summary>Gets the optional native address of <c>il2cpp_type_get_type</c>.</summary>
    public nint? TypeGetType { get; }
    /// <summary>Gets an optional native class-from-type entry point, resolved from supported public export aliases.</summary>
    public nint? ClassFromType { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_enum_basetype</c>.</summary>
    public nint? ClassEnumBaseType { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_valuetype</c>.</summary>
    public nint? ClassIsValueType { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_enum</c>.</summary>
    public nint? ClassIsEnum { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_blittable</c>.</summary>
    public nint? ClassIsBlittable { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_generic</c>.</summary>
    public nint? ClassIsGeneric { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_inflated</c>.</summary>
    public nint? ClassIsInflated { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_flags</c>.</summary>
    public nint? ClassGetFlags { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_value_size</c>.</summary>
    public nint? ClassValueSize { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_type_token</c>.</summary>
    public nint? ClassGetTypeToken { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_instance_size</c>.</summary>
    public nint? ClassInstanceSize { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_parent</c>.</summary>
    public nint? ClassGetParent { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_interfaces</c>.</summary>
    public nint? ClassGetInterfaces { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_nested_types</c>.</summary>
    public nint? ClassGetNestedTypes { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_declaring_type</c>.</summary>
    public nint? ClassGetDeclaringType { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_image</c>.</summary>
    public nint? ClassGetImage { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_method_get_flags</c>.</summary>
    public nint? MethodGetFlags { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_method_is_generic</c>.</summary>
    public nint? MethodIsGeneric { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_method_is_inflated</c>.</summary>
    public nint? MethodIsInflated { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_method_get_token</c>.</summary>
    public nint? MethodGetToken { get; }

    /// <summary>Gets the optional native address of <c>il2cpp_string_length</c>.</summary>
    public nint? StringLength { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_string_chars</c>.</summary>
    public nint? StringChars { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_array_length</c>.</summary>
    public nint? ArrayLength { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_array_get_byte_length</c>.</summary>
    public nint? ArrayGetByteLength { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_array_element_size</c>.</summary>
    public nint? ArrayElementSize { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_element_class</c>.</summary>
    public nint? ClassGetElementClass { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_get_type</c>.</summary>
    public nint? ClassGetType { get; }

    /// <summary>Gets the optional native address of <c>il2cpp_runtime_invoke</c>.</summary>
    public nint? RuntimeInvoke { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_object_unbox</c>.</summary>
    public nint? ObjectUnbox { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_thread_attach</c>.</summary>
    public nint? ThreadAttach { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_thread_detach</c>.</summary>
    public nint? ThreadDetach { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_method_is_instance</c>.</summary>
    public nint? MethodIsInstance { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_object_get_class</c>.</summary>
    public nint? ObjectGetClass { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_object_get_virtual_method</c>.</summary>
    public nint? ObjectGetVirtualMethod { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_class_is_assignable_from</c>.</summary>
    public nint? ClassIsAssignableFrom { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_gchandle_new</c>.</summary>
    public nint? GcHandleNew { get; }
    /// <summary>Gets the optional native address of <c>il2cpp_gchandle_free</c>.</summary>
    public nint? GcHandleFree { get; }

    /// <summary>Initializes the complete export snapshot from one already parsed GameAssembly image.</summary>
    /// <param name="image">The loaded GameAssembly PE image whose direct exports should be resolved.</param>
    private Il2CppRuntimeExports(PeImage image)
    {
        DomainGet = ResolveRequiredExport(image, DomainGetExportName);
        DomainGetAssemblies = ResolveRequiredExport(image, DomainGetAssembliesExportName);
        AssemblyGetImage = ResolveRequiredExport(image, AssemblyGetImageExportName);
        ImageGetName = ResolveRequiredExport(image, ImageGetNameExportName);
        ClassFromName = ResolveRequiredExport(image, ClassFromNameExportName);
        ClassGetMethods = ResolveRequiredExport(image, ClassGetMethodsExportName);
        MethodGetName = ResolveRequiredExport(image, MethodGetNameExportName);
        MethodGetParamCount = ResolveRequiredExport(image, MethodGetParamCountExportName);
        MethodGetParam = ResolveRequiredExport(image, MethodGetParamExportName);
        MethodGetReturnType = ResolveRequiredExport(image, MethodGetReturnTypeExportName);
        TypeGetName = ResolveRequiredExport(image, TypeGetNameExportName);
        ClassGetFields = ResolveRequiredExport(image, ClassGetFieldsExportName);
        FieldGetName = ResolveRequiredExport(image, FieldGetNameExportName);
        FieldGetType = ResolveRequiredExport(image, FieldGetTypeExportName);
        FieldGetFlags = ResolveRequiredExport(image, FieldGetFlagsExportName);
        FieldGetOffset = ResolveRequiredExport(image, FieldGetOffsetExportName);
        Free = ResolveRequiredExport(image, FreeExportName);

        ClassGetProperties = ResolveOptionalExport(image, ClassGetPropertiesExportName);
        PropertyGetName = ResolveOptionalExport(image, PropertyGetNameExportName);
        PropertyGetFlags = ResolveOptionalExport(image, PropertyGetFlagsExportName);
        PropertyGetGetMethod = ResolveOptionalExport(image, PropertyGetGetMethodExportName);
        PropertyGetSetMethod = ResolveOptionalExport(image, PropertyGetSetMethodExportName);
        ImageGetClassCount = ResolveOptionalExport(image, ImageGetClassCountExportName);
        ImageGetClass = ResolveOptionalExport(image, ImageGetClassExportName);
        ClassGetName = ResolveOptionalExport(image, ClassGetNameExportName);
        ClassGetNamespace = ResolveOptionalExport(image, ClassGetNamespaceExportName);
        ClassGetStaticFieldData = ResolveOptionalExport(image, ClassGetStaticFieldDataExportName);
        ClassGetDataSize = ResolveOptionalExport(image, ClassGetDataSizeExportName);
        TypeGetType = ResolveOptionalExport(image, TypeGetTypeExportName);
        ClassFromType = ResolveFirstOptionalExport(image, ClassFromTypeExportName, ClassFromIl2CppTypeExportName);
        ClassEnumBaseType = ResolveOptionalExport(image, ClassEnumBaseTypeExportName);
        ClassIsValueType = ResolveOptionalExport(image, ClassIsValueTypeExportName);
        ClassIsEnum = ResolveOptionalExport(image, ClassIsEnumExportName);
        ClassIsBlittable = ResolveOptionalExport(image, ClassIsBlittableExportName);
        ClassIsGeneric = ResolveOptionalExport(image, ClassIsGenericExportName);
        ClassIsInflated = ResolveOptionalExport(image, ClassIsInflatedExportName);
        ClassGetFlags = ResolveOptionalExport(image, ClassGetFlagsExportName);
        ClassValueSize = ResolveOptionalExport(image, ClassValueSizeExportName);
        ClassGetTypeToken = ResolveOptionalExport(image, ClassGetTypeTokenExportName);
        ClassInstanceSize = ResolveOptionalExport(image, ClassInstanceSizeExportName);
        ClassGetParent = ResolveOptionalExport(image, ClassGetParentExportName);
        ClassGetInterfaces = ResolveOptionalExport(image, ClassGetInterfacesExportName);
        ClassGetNestedTypes = ResolveOptionalExport(image, ClassGetNestedTypesExportName);
        ClassGetDeclaringType = ResolveOptionalExport(image, ClassGetDeclaringTypeExportName);
        ClassGetImage = ResolveOptionalExport(image, ClassGetImageExportName);
        MethodGetFlags = ResolveOptionalExport(image, MethodGetFlagsExportName);
        MethodIsGeneric = ResolveOptionalExport(image, MethodIsGenericExportName);
        MethodIsInflated = ResolveOptionalExport(image, MethodIsInflatedExportName);
        MethodGetToken = ResolveOptionalExport(image, MethodGetTokenExportName);
        StringLength = ResolveOptionalExport(image, StringLengthExportName);
        StringChars = ResolveOptionalExport(image, StringCharsExportName);
        ArrayLength = ResolveOptionalExport(image, ArrayLengthExportName);
        ArrayGetByteLength = ResolveOptionalExport(image, ArrayGetByteLengthExportName);
        ArrayElementSize = ResolveOptionalExport(image, ArrayElementSizeExportName);
        ClassGetElementClass = ResolveOptionalExport(image, ClassGetElementClassExportName);
        ClassGetType = ResolveOptionalExport(image, ClassGetTypeExportName);
        RuntimeInvoke = ResolveOptionalExport(image, RuntimeInvokeExportName);
        ObjectUnbox = ResolveOptionalExport(image, ObjectUnboxExportName);
        ThreadAttach = ResolveOptionalExport(image, ThreadAttachExportName);
        ThreadDetach = ResolveOptionalExport(image, ThreadDetachExportName);
        MethodIsInstance = ResolveOptionalExport(image, MethodIsInstanceExportName);
        ObjectGetClass = ResolveOptionalExport(image, ObjectGetClassExportName);
        ObjectGetVirtualMethod = ResolveOptionalExport(image, ObjectGetVirtualMethodExportName);
        ClassIsAssignableFrom = ResolveOptionalExport(image, ClassIsAssignableFromExportName);
        GcHandleNew = ResolveOptionalExport(image, GcHandleNewExportName);
        GcHandleFree = ResolveOptionalExport(image, GcHandleFreeExportName);
    }

    /// <summary>Resolves all required exports and discovers every optional capability exposed by the target GameAssembly.</summary>
    /// <param name="target">The validated IL2CPP target whose export table should be inspected.</param>
    /// <returns>A strongly typed runtime export snapshot.</returns>
    /// <exception cref="InvalidDataException">Thrown when a required export is unavailable as a direct address inside GameAssembly.</exception>
    public static Il2CppRuntimeExports Resolve(Il2CppTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new Il2CppRuntimeExports(target.GameAssemblyImage);
    }

    /// <summary>Resolves a required direct IL2CPP export and validates that its address belongs to the loaded image.</summary>
    /// <param name="image">The parsed GameAssembly image.</param>
    /// <param name="exportName">The exact export name to resolve.</param>
    /// <returns>The validated native runtime address.</returns>
    private static nint ResolveRequiredExport(PeImage image, string exportName)
    {
        nint? address = ResolveOptionalExport(image, exportName);

        if (address is null)
            throw new InvalidDataException($"Required IL2CPP runtime export '{exportName}' was not found as a direct address inside GameAssembly.");

        return address.Value;
    }

    /// <summary>Resolves the first available direct export from a list of compatible public API aliases.</summary>
    /// <param name="image">The parsed GameAssembly image.</param>
    /// <param name="exportNames">The compatible export names in preferred order.</param>
    /// <returns>The first validated address, or <see langword="null"/> when none is available.</returns>
    private static nint? ResolveFirstOptionalExport(PeImage image, params string[] exportNames)
    {
        foreach (string exportName in exportNames)
        {
            nint? address = ResolveOptionalExport(image, exportName);

            if (address is not null)
                return address;
        }

        return null;
    }

    /// <summary>Resolves an optional direct IL2CPP export when present and valid.</summary>
    /// <param name="image">The parsed GameAssembly image.</param>
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
