using System;

namespace TypeSafe.AI;

/// <summary>
/// A typed handle to a question in a <see cref="QuestionSet"/>, used to read its answer with the
/// matching type through <see cref="AnswerSet.Get{TAnswer}(QuestionKey{TAnswer})"/>.
/// </summary>
/// <typeparam name="TAnswer">The answer type for the question.</typeparam>
public readonly struct QuestionKey<TAnswer> : IEquatable<QuestionKey<TAnswer>>
    where TAnswer : Answer
{
    private readonly string? _name;
    private readonly Func<Answer, string, TAnswer>? _convert;

    internal QuestionKey(string name, Func<Answer, string, TAnswer> convert)
    {
        _name = name;
        _convert = convert;
    }

    /// <summary>Gets the question name.</summary>
    public string Name => _name ?? string.Empty;

    /// <summary>Determines whether two keys refer to the same question name.</summary>
    public static bool operator ==(QuestionKey<TAnswer> left, QuestionKey<TAnswer> right) => left.Equals(right);

    /// <summary>Determines whether two keys refer to different question names.</summary>
    public static bool operator !=(QuestionKey<TAnswer> left, QuestionKey<TAnswer> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(QuestionKey<TAnswer> other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is QuestionKey<TAnswer> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <inheritdoc/>
    public override string ToString() => Name;

    internal TAnswer Convert(Answer answer)
        => _convert is null ? AnswerConversion.As<TAnswer>(answer, Name) : _convert(answer, Name);
}
