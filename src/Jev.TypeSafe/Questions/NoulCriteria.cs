namespace TypeSafe.AI;

/// <summary>Optional descriptions of the outcomes of a yes/no question. Either side may be described alone.</summary>
public sealed class NoulCriteria
{
    /// <summary>Gets the description of the yes outcome; omitted entries are not sent.</summary>
    public Entry True { get; init; }

    /// <summary>Gets the description of the no outcome; omitted entries are not sent.</summary>
    public Entry False { get; init; }
}
