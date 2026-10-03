using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// Named questions in insertion order. Adding a question returns a typed <see cref="QuestionKey{TAnswer}"/>
/// for reading its answer.
/// </summary>
/// <remarks>A set may be reused across requests; the SDK never modifies it.</remarks>
public sealed class QuestionSet : IReadOnlyCollection<KeyValuePair<string, Question>>
{
    private readonly List<KeyValuePair<string, Question>> _items = [];
    private readonly Dictionary<string, Question> _byName = new(StringComparer.Ordinal);

    /// <summary>Gets the number of questions.</summary>
    public int Count => _items.Count;

    /// <summary>Adds a yes/no question.</summary>
    /// <param name="name">The name that identifies the answer; any string, sent verbatim.</param>
    /// <param name="question">The question.</param>
    /// <returns>A key for reading the answer.</returns>
    /// <exception cref="ArgumentException">The name is already used.</exception>
    public QuestionKey<NoulAnswer> Add(string name, NoulQuestion question)
        => AddCore(name, question, AnswerConversion.As<NoulAnswer>);

    /// <summary>Adds a choice question.</summary>
    /// <param name="name">The name that identifies the answer; any string, sent verbatim.</param>
    /// <param name="question">The question.</param>
    /// <returns>A key for reading the answer.</returns>
    /// <exception cref="ArgumentException">The name is already used.</exception>
    public QuestionKey<ChoiceAnswer> Add(string name, ChoiceQuestion question)
        => AddCore(name, question, AnswerConversion.As<ChoiceAnswer>);

    /// <summary>Adds a choice question whose labels are enum members.</summary>
    /// <typeparam name="TEnum">The enum that defines the labels.</typeparam>
    /// <param name="name">The name that identifies the answer; any string, sent verbatim.</param>
    /// <param name="question">The question.</param>
    /// <returns>A key for reading the answer as <typeparamref name="TEnum"/>.</returns>
    /// <exception cref="ArgumentException">The name is already used.</exception>
    public QuestionKey<ChoiceAnswer<TEnum>> Add<TEnum>(string name, ChoiceQuestion<TEnum> question)
        where TEnum : struct, Enum
        => AddCore(name, question, AnswerConversion.AsEnumChoice<TEnum>);

    /// <summary>Adds a score question.</summary>
    /// <param name="name">The name that identifies the answer; any string, sent verbatim.</param>
    /// <param name="question">The question.</param>
    /// <returns>A key for reading the answer.</returns>
    /// <exception cref="ArgumentException">The name is already used.</exception>
    public QuestionKey<ScoreAnswer> Add(string name, ScoreQuestion question)
        => AddCore(name, question, AnswerConversion.As<ScoreAnswer>);

    /// <summary>Determines whether a question with the name exists.</summary>
    /// <param name="name">The question name.</param>
    /// <returns><see langword="true"/> when the name is used.</returns>
    public bool Contains(string name) => _byName.ContainsKey(Guard.NotNull(name));

    /// <summary>Gets a question by name.</summary>
    /// <param name="name">The question name.</param>
    /// <param name="question">The question, when found.</param>
    /// <returns><see langword="true"/> when the name is used.</returns>
    public bool TryGetQuestion(string name, [NotNullWhen(true)] out Question? question)
        => _byName.TryGetValue(Guard.NotNull(name), out question);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, Question>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private QuestionKey<TAnswer> AddCore<TAnswer>(string name, Question question, Func<Answer, string, TAnswer> convert)
        where TAnswer : Answer
    {
        Guard.NotNull(name);
        Guard.NotNull(question);
        if (_byName.ContainsKey(name))
        {
            throw new ArgumentException($"A question named '{name}' has already been added.", nameof(name));
        }

        _byName.Add(name, question);
        _items.Add(new KeyValuePair<string, Question>(name, question));
        return new QuestionKey<TAnswer>(name, convert);
    }
}
