namespace UnityIl2CppResolver.Il2Cpp.Queries;

/// <summary>
/// Describes the semantic identity of an IL2CPP assembly requested by a resolver consumer.
/// The query accepts either the managed simple assembly name or the corresponding runtime image name with a conventional <c>.dll</c> or <c>.exe</c> suffix.
/// It contains no native addresses and therefore remains independent from every concrete resolution backend.
/// </summary>
public sealed record AssemblyQuery
{
    /// <summary>
    /// Gets the semantic assembly name supplied by the resolver consumer.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a semantic assembly query.
    /// </summary>
    /// <param name="name">The managed simple assembly name or runtime image name to resolve.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or contains only whitespace.
    /// </exception>
    public AssemblyQuery(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }
}