using System;
using System.Collections;
using System.Collections.Generic;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// Choice labels mapped to descriptions, kept in insertion order. Supports collection initializers:
/// <c>new ChoiceCriteria { { "billing", "Payments and invoices" }, { "other", Entry.Null } }</c>.
/// </summary>
/// <remarks>Criteria are copied when a question is created, so later changes do not affect existing questions.</remarks>
public sealed class ChoiceCriteria : IReadOnlyList<KeyValuePair<string, Entry>>
{
    private readonly List<KeyValuePair<string, Entry>> _items = [];
    private readonly Dictionary<string, int> _indexes = new(StringComparer.Ordinal);
    private bool _isFrozen;

    /// <summary>Gets the number of labels.</summary>
    public int Count => _items.Count;

    /// <summary>Gets the label and description at the specified position.</summary>
    /// <param name="index">The zero-based position.</param>
    public KeyValuePair<string, Entry> this[int index] => _items[index];

    /// <summary>Creates criteria from labels, each described as <c>null</c>.</summary>
    /// <param name="labels">The labels, in order.</param>
    /// <returns>The criteria.</returns>
    /// <exception cref="ArgumentException">A label is duplicated.</exception>
    public static ChoiceCriteria FromLabels(params string[] labels)
    {
        var criteria = new ChoiceCriteria();
        foreach (string label in Guard.NotNull(labels))
        {
            criteria.Add(label, Entry.Null);
        }

        return criteria;
    }

    /// <summary>Adds a label and its description.</summary>
    /// <param name="label">The label, sent verbatim.</param>
    /// <param name="description">The description; omitted descriptions are sent as <c>null</c>.</param>
    /// <exception cref="ArgumentException">The label is already present.</exception>
    /// <exception cref="InvalidOperationException">These criteria belong to a question and are read-only.</exception>
    public void Add(string label, Entry description)
    {
        Guard.NotNull(label);
        if (_isFrozen)
        {
            throw new InvalidOperationException("Criteria that belong to a question cannot be modified.");
        }

        if (_indexes.ContainsKey(label))
        {
            throw new ArgumentException($"The label '{label}' has already been added.", nameof(label));
        }

        _indexes.Add(label, _items.Count);
        _items.Add(new KeyValuePair<string, Entry>(label, description));
    }

    /// <summary>Gets the description of a label.</summary>
    /// <param name="label">The label.</param>
    /// <param name="description">The description, when found.</param>
    /// <returns><see langword="true"/> when the label exists.</returns>
    public bool TryGetDescription(string label, out Entry description)
    {
        if (_indexes.TryGetValue(Guard.NotNull(label), out int index))
        {
            description = _items[index].Value;
            return true;
        }

        description = default;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, Entry>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static ChoiceCriteria FromEnum<TEnum>(IReadOnlyDictionary<TEnum, Entry>? descriptions)
        where TEnum : struct, Enum
    {
        EnumLabelMap<TEnum> map = EnumLabelMap<TEnum>.Instance;
        if (descriptions is not null)
        {
            foreach (TEnum value in descriptions.Keys)
            {
                if (!map.Contains(value))
                {
                    throw new ArgumentException($"'{value}' is not a defined member of {typeof(TEnum).Name}.", nameof(descriptions));
                }
            }
        }

        var criteria = new ChoiceCriteria();
        foreach (KeyValuePair<TEnum, string> member in map.Members)
        {
            Entry description = descriptions is not null && descriptions.TryGetValue(member.Key, out Entry found) ? found : Entry.Null;
            criteria.Add(member.Value, description);
        }

        criteria._isFrozen = true;
        return criteria;
    }

    internal ChoiceCriteria ToFrozenCopy()
    {
        if (_isFrozen)
        {
            return this;
        }

        var copy = new ChoiceCriteria();
        foreach (KeyValuePair<string, Entry> item in _items)
        {
            copy.Add(item.Key, item.Value);
        }

        copy._isFrozen = true;
        return copy;
    }
}
