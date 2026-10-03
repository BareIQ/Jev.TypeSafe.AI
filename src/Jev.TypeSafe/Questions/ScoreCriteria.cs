using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>An immutable rubric: descriptions indexed by score from zero.</summary>
public sealed class ScoreCriteria : IReadOnlyList<Entry>
{
    private readonly ImmutableArray<Entry> _descriptions;

    /// <summary>Initializes a new instance of the <see cref="ScoreCriteria"/> class.</summary>
    /// <param name="descriptions">Descriptions in score order; omitted entries are sent as <c>null</c>.</param>
    public ScoreCriteria(IEnumerable<Entry> descriptions)
        => _descriptions = [.. Guard.NotNull(descriptions)];

    /// <summary>Gets the number of scores.</summary>
    public int Count => _descriptions.Length;

    /// <summary>Gets the description of a score.</summary>
    /// <param name="index">The score.</param>
    public Entry this[int index] => _descriptions[index];

    /// <summary>Converts descriptions to criteria.</summary>
    /// <param name="descriptions">Descriptions in score order.</param>
    public static implicit operator ScoreCriteria(Entry[] descriptions) => FromEntries(descriptions);

    /// <summary>Converts text descriptions to criteria.</summary>
    /// <param name="descriptions">Text descriptions in score order.</param>
    public static implicit operator ScoreCriteria(string[] descriptions) => FromTexts(descriptions);

    /// <summary>Creates criteria from descriptions.</summary>
    /// <param name="descriptions">Descriptions in score order.</param>
    /// <returns>The criteria.</returns>
    public static ScoreCriteria FromEntries(params Entry[] descriptions) => new(Guard.NotNull(descriptions));

    /// <summary>Creates criteria from text descriptions.</summary>
    /// <param name="descriptions">Text descriptions in score order.</param>
    /// <returns>The criteria.</returns>
    public static ScoreCriteria FromTexts(params string[] descriptions)
        => new(Guard.NotNull(descriptions).Select(Entry.FromText));

    /// <inheritdoc/>
    public IEnumerator<Entry> GetEnumerator() => ((IEnumerable<Entry>)_descriptions).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
