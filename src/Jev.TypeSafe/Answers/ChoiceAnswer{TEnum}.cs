using System;
using System.Collections.Generic;
using TypeSafe.AI.Internal.Collections;

namespace TypeSafe.AI;

/// <summary>The answer to a choice question whose labels are the members of <typeparamref name="TEnum"/>.</summary>
/// <typeparam name="TEnum">The enum that defines the labels.</typeparam>
public sealed class ChoiceAnswer<TEnum> : ChoiceAnswer
    where TEnum : struct, Enum
{
    private ChoiceAnswer(ChoiceAnswer source, TEnum value, IReadOnlyDictionary<TEnum, double> probabilities)
        : base(source.Raw, source.Choice, source.Confidence, source.Probabilities)
    {
        Value = value;
        ProbabilitiesByValue = probabilities;
    }

    /// <summary>Gets the selected member.</summary>
    public TEnum Value { get; }

    /// <summary>Gets probabilities keyed by member, in server order.</summary>
    public IReadOnlyDictionary<TEnum, double> ProbabilitiesByValue { get; }

    /// <summary>Maps an untyped choice answer onto <typeparamref name="TEnum"/>.</summary>
    /// <exception cref="TypeSafeException">A label does not match any member.</exception>
    internal static ChoiceAnswer<TEnum> From(ChoiceAnswer answer, string questionName)
    {
        if (answer is ChoiceAnswer<TEnum> typed)
        {
            return typed;
        }

        EnumLabelMap<TEnum> map = EnumLabelMap<TEnum>.Instance;
        var probabilities = new List<KeyValuePair<TEnum, double>>(answer.Probabilities.Count);
        foreach (KeyValuePair<string, double> probability in answer.Probabilities)
        {
            probabilities.Add(new KeyValuePair<TEnum, double>(ToValue(map, probability.Key, questionName), probability.Value));
        }

        return new ChoiceAnswer<TEnum>(
            answer,
            ToValue(map, answer.Choice, questionName),
            new OrderedReadOnlyDictionary<TEnum, double>(probabilities));
    }

    private static TEnum ToValue(EnumLabelMap<TEnum> map, string label, string questionName)
        => map.TryGetValue(label, out TEnum value)
            ? value
            : throw new TypeSafeException(
                $"The label '{label}' in the answer to '{questionName}' does not match any member of {typeof(TEnum).Name}.");
}
