using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace TypeSafe.AI;

/// <summary>Bidirectional mapping between enum members and choice labels, computed once per enum type.</summary>
internal sealed class EnumLabelMap<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Lazy<EnumLabelMap<TEnum>> s_instance = new(Build);

    private readonly Dictionary<TEnum, string> _labelsByValue;
    private readonly Dictionary<string, TEnum> _valuesByLabel;

    private EnumLabelMap(List<KeyValuePair<TEnum, string>> members)
    {
        Members = members;
        _labelsByValue = [];
        _valuesByLabel = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (KeyValuePair<TEnum, string> member in members)
        {
            if (_valuesByLabel.ContainsKey(member.Value))
            {
                throw new ArgumentException($"{typeof(TEnum).Name} maps more than one member to the label '{member.Value}'.");
            }

            _valuesByLabel.Add(member.Value, member.Key);
            if (!_labelsByValue.ContainsKey(member.Key))
            {
                _labelsByValue.Add(member.Key, member.Value);
            }
        }
    }

    /// <summary>Gets the map for <typeparamref name="TEnum"/>.</summary>
    /// <exception cref="ArgumentException">The enum is a flags enum, has no members, or has duplicate labels.</exception>
    public static EnumLabelMap<TEnum> Instance => s_instance.Value;

    /// <summary>Gets members and labels in declaration order.</summary>
    public IReadOnlyList<KeyValuePair<TEnum, string>> Members { get; }

    public bool Contains(TEnum value) => _labelsByValue.ContainsKey(value);

    public bool TryGetValue(string label, out TEnum value) => _valuesByLabel.TryGetValue(label, out value);

    private static EnumLabelMap<TEnum> Build()
    {
        Type type = typeof(TEnum);
        if (type.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            throw new ArgumentException($"{type.Name} is a flags enum; choice labels require a non-flags enum.");
        }

        var members = new List<KeyValuePair<TEnum, string>>();
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is TEnum value)
            {
                string label = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
                members.Add(new KeyValuePair<TEnum, string>(value, label));
            }
        }

        return members.Count == 0
            ? throw new ArgumentException($"{type.Name} has no members to use as choice labels.")
            : new EnumLabelMap<TEnum>(members);
    }
}
