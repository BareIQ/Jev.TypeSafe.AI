using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TypeSafe.AI.Internal.Collections;

/// <summary>
/// An immutable dictionary that enumerates in insertion order. Duplicate keys keep the first
/// position and the last value, matching JSON object semantics.
/// </summary>
internal sealed class OrderedReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly List<KeyValuePair<TKey, TValue>> _items;
    private readonly Dictionary<TKey, int> _indexes;

    public OrderedReadOnlyDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey>? comparer = null)
    {
        _items = [];
        _indexes = new Dictionary<TKey, int>(comparer);
        foreach (KeyValuePair<TKey, TValue> item in items)
        {
            if (_indexes.TryGetValue(item.Key, out int index))
            {
                _items[index] = new KeyValuePair<TKey, TValue>(_items[index].Key, item.Value);
            }
            else
            {
                _indexes.Add(item.Key, _items.Count);
                _items.Add(item);
            }
        }
    }

    public static OrderedReadOnlyDictionary<TKey, TValue> Empty { get; } = new([]);

    public int Count => _items.Count;

    public IEnumerable<TKey> Keys
    {
        get
        {
            foreach (KeyValuePair<TKey, TValue> item in _items)
            {
                yield return item.Key;
            }
        }
    }

    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (KeyValuePair<TKey, TValue> item in _items)
            {
                yield return item.Value;
            }
        }
    }

    public TValue this[TKey key] => TryGetValue(key, out TValue? value)
        ? value
        : throw new KeyNotFoundException($"The key '{key}' was not found.");

    public bool ContainsKey(TKey key) => _indexes.ContainsKey(key);

#pragma warning disable CS8767 // netstandard2.0's IReadOnlyDictionary lacks [MaybeNullWhen]; the annotation here is the accurate contract.
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
#pragma warning restore CS8767
    {
        if (_indexes.TryGetValue(key, out int index))
        {
            value = _items[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
