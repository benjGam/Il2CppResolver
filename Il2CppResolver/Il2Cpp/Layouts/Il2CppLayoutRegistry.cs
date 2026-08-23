namespace UnityIl2CppResolver.Il2Cpp.Layouts;

/// <summary>
/// Stores the structural compatibility profiles available to automatic layout detection for one resolver session.
/// The registry rejects duplicate structural identities because equivalent candidates would make detection artificially ambiguous even when their diagnostic names differ.
/// </summary>
internal sealed class Il2CppLayoutRegistry
{
    /// <summary>
    /// Stores registered class-layout profiles in deterministic registration order.
    /// </summary>
    private readonly List<Il2CppClassLayout> _classLayouts = new();

    /// <summary>
    /// Stores registered method-layout profiles in deterministic registration order.
    /// </summary>
    private readonly List<Il2CppMethodInfoLayout> _methodInfoLayouts = new();

    /// <summary>
    /// Initializes a registry with the supplied built-in class and method layouts.
    /// </summary>
    /// <param name="classLayouts">The class layouts registered initially.</param>
    /// <param name="methodInfoLayouts">The method layouts registered initially.</param>
    public Il2CppLayoutRegistry(IEnumerable<Il2CppClassLayout> classLayouts, IEnumerable<Il2CppMethodInfoLayout> methodInfoLayouts)
    {
        ArgumentNullException.ThrowIfNull(classLayouts);
        ArgumentNullException.ThrowIfNull(methodInfoLayouts);

        foreach (Il2CppClassLayout layout in classLayouts)
            RegisterClassLayout(layout);

        foreach (Il2CppMethodInfoLayout layout in methodInfoLayouts)
            RegisterMethodInfoLayout(layout);
    }

    /// <summary>
    /// Gets an immutable snapshot of the currently registered class-layout candidates.
    /// </summary>
    /// <returns>The registered class-layout profiles in deterministic order.</returns>
    public IReadOnlyList<Il2CppClassLayout> GetClassLayouts()
    {
        return _classLayouts.ToArray();
    }

    /// <summary>
    /// Gets an immutable snapshot of the currently registered method-layout candidates.
    /// </summary>
    /// <returns>The registered method-layout profiles in deterministic order.</returns>
    public IReadOnlyList<Il2CppMethodInfoLayout> GetMethodInfoLayouts()
    {
        return _methodInfoLayouts.ToArray();
    }

    /// <summary>
    /// Registers one class-layout candidate when neither its name nor structural offsets conflict with an existing candidate.
    /// Re-registering the exact same definition is treated as an idempotent no-op.
    /// </summary>
    /// <param name="layout">The class-layout profile to register.</param>
    /// <returns><see langword="true"/> when the profile was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterClassLayout(Il2CppClassLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        foreach (Il2CppClassLayout existing in _classLayouts)
        {
            bool sameName = string.Equals(existing.Name, layout.Name, StringComparison.Ordinal);
            bool sameStructure = existing.StaticFieldsPointerOffset == layout.StaticFieldsPointerOffset && existing.StaticFieldsSizeOffset == layout.StaticFieldsSizeOffset;

            if (sameName && sameStructure)
                return false;

            if (sameName)
                throw new ArgumentException($"A class layout named '{layout.Name}' is already registered with different structural offsets.", nameof(layout));

            if (sameStructure)
                throw new ArgumentException($"Class layout '{layout.Name}' duplicates the structural identity of registered profile '{existing.Name}'.", nameof(layout));
        }

        _classLayouts.Add(layout);
        return true;
    }

    /// <summary>
    /// Registers one method-layout candidate when neither its name nor structural offset conflicts with an existing candidate.
    /// Re-registering the exact same definition is treated as an idempotent no-op.
    /// </summary>
    /// <param name="layout">The method-layout profile to register.</param>
    /// <returns><see langword="true"/> when the profile was added; <see langword="false"/> when the exact definition was already registered.</returns>
    public bool RegisterMethodInfoLayout(Il2CppMethodInfoLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        foreach (Il2CppMethodInfoLayout existing in _methodInfoLayouts)
        {
            bool sameName = string.Equals(existing.Name, layout.Name, StringComparison.Ordinal);
            bool sameStructure = existing.DirectMethodPointerOffset == layout.DirectMethodPointerOffset;

            if (sameName && sameStructure)
                return false;

            if (sameName)
                throw new ArgumentException($"A MethodInfo layout named '{layout.Name}' is already registered with a different structural offset.", nameof(layout));

            if (sameStructure)
                throw new ArgumentException($"MethodInfo layout '{layout.Name}' duplicates the structural identity of registered profile '{existing.Name}'.", nameof(layout));
        }

        _methodInfoLayouts.Add(layout);
        return true;
    }
}
