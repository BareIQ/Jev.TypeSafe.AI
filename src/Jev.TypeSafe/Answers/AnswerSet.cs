using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>Answers keyed by question name, in server order.</summary>
public sealed class AnswerSet : IReadOnlyDictionary<string, Answer>
{
    private readonly OrderedReadOnlyDictionary<string, Answer> _answers;

    internal AnswerSet(OrderedReadOnlyDictionary<string, Answer> answers) => _answers = answers;

    /// <inheritdoc/>
    public int Count => _answers.Count;

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _answers.Keys;

    /// <inheritdoc/>
    public IEnumerable<Answer> Values => _answers.Values;

    /// <summary>Gets the answer to a question.</summary>
    /// <param name="key">The question name.</param>
    /// <exception cref="KeyNotFoundException">There is no answer with the name.</exception>
    public Answer this[string key] => _answers.TryGetValue(Guard.NotNull(key), out Answer? answer)
        ? answer
        : throw new KeyNotFoundException($"There is no answer to a question named '{key}'.");

    /// <summary>Gets the typed answer for a question key.</summary>
    /// <typeparam name="TAnswer">The answer type.</typeparam>
    /// <param name="key">The key returned when the question was added.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="KeyNotFoundException">There is no answer with the key's name.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    /// <exception cref="TypeSafeException">An enum choice label does not match the enum.</exception>
    public TAnswer Get<TAnswer>(QuestionKey<TAnswer> key)
        where TAnswer : Answer
        => key.Convert(this[key.Name]);

    /// <summary>Gets the typed answer for a question key, if present.</summary>
    /// <typeparam name="TAnswer">The answer type.</typeparam>
    /// <param name="key">The key returned when the question was added.</param>
    /// <param name="answer">The answer, when present.</param>
    /// <returns><see langword="true"/> when an answer exists for the key's name.</returns>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    public bool TryGet<TAnswer>(QuestionKey<TAnswer> key, [NotNullWhen(true)] out TAnswer? answer)
        where TAnswer : Answer
    {
        if (_answers.TryGetValue(key.Name, out Answer? found))
        {
            answer = key.Convert(found);
            return true;
        }

        answer = null;
        return false;
    }

    /// <summary>Gets a yes/no answer by name.</summary>
    /// <param name="name">The question name.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="KeyNotFoundException">There is no answer with the name.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    public NoulAnswer GetNoul(string name) => AnswerConversion.As<NoulAnswer>(this[name], name);

    /// <summary>Gets a choice answer by name.</summary>
    /// <param name="name">The question name.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="KeyNotFoundException">There is no answer with the name.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    public ChoiceAnswer GetChoice(string name) => AnswerConversion.As<ChoiceAnswer>(this[name], name);

    /// <summary>Gets a choice answer by name, mapped onto <typeparamref name="TEnum"/>.</summary>
    /// <typeparam name="TEnum">The enum that defines the labels.</typeparam>
    /// <param name="name">The question name.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="KeyNotFoundException">There is no answer with the name.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    /// <exception cref="TypeSafeException">A label does not match the enum.</exception>
    public ChoiceAnswer<TEnum> GetChoice<TEnum>(string name)
        where TEnum : struct, Enum
        => AnswerConversion.AsEnumChoice<TEnum>(this[name], name);

    /// <summary>Gets a score answer by name.</summary>
    /// <param name="name">The question name.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="KeyNotFoundException">There is no answer with the name.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    public ScoreAnswer GetScore(string name) => AnswerConversion.As<ScoreAnswer>(this[name], name);

    /// <inheritdoc/>
    public bool ContainsKey(string key) => _answers.ContainsKey(Guard.NotNull(key));

    /// <inheritdoc/>
#pragma warning disable CS8767 // netstandard2.0's IReadOnlyDictionary lacks [MaybeNullWhen]; the annotation here is the accurate contract.
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Answer value)
#pragma warning restore CS8767
        => _answers.TryGetValue(Guard.NotNull(key), out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, Answer>> GetEnumerator() => _answers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
