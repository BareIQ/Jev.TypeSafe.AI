using System;
using System.Collections.Generic;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// A question to answer about the request state. Create instances with <see cref="Noul"/>,
/// <see cref="Choice(Entry, ChoiceCriteria)"/>, <see cref="Choice{TEnum}(Entry)"/>, or <see cref="Score(Entry, ScoreCriteria)"/>.
/// </summary>
/// <remarks>Questions are immutable and may be reused across requests.</remarks>
public abstract class Question
{
    private protected Question(Entry instructions) => Instructions = instructions;

    /// <summary>Gets the kind of question.</summary>
    public abstract QuestionType Type { get; }

    /// <summary>Gets the question as text, JSON, or <c>null</c>; omitted when <see cref="Entry.IsOmitted"/>.</summary>
    public Entry Instructions { get; }

    /// <summary>Creates a yes/no question with optional descriptions of either outcome.</summary>
    /// <param name="instructions">The question; omitted instructions are sent as <c>null</c>.</param>
    /// <param name="criteria">Optional outcome descriptions; <see langword="null"/> leaves criteria out of the request.</param>
    /// <returns>The question.</returns>
    public static NoulQuestion Noul(Entry instructions = default, NoulCriteria? criteria = null)
        => new(instructions.IsOmitted ? Entry.Null : instructions, criteria);

    /// <summary>Creates a question that selects between named labels.</summary>
    /// <param name="instructions">The question.</param>
    /// <param name="criteria">Labels mapped to descriptions. The criteria are copied.</param>
    /// <returns>The question.</returns>
    public static ChoiceQuestion Choice(Entry instructions, ChoiceCriteria criteria)
        => new(instructions, Guard.NotNull(criteria).ToFrozenCopy());

    /// <summary>
    /// Creates a question whose labels are the members of <typeparamref name="TEnum"/>, each described as <c>null</c>.
    /// A member's label is its name, or the value of <see cref="System.Runtime.Serialization.EnumMemberAttribute"/> when present.
    /// </summary>
    /// <typeparam name="TEnum">A non-flags enum type with at least one member.</typeparam>
    /// <param name="instructions">The question.</param>
    /// <returns>The question.</returns>
    /// <exception cref="ArgumentException">The enum is a flags enum, has no members, or maps two members to one label.</exception>
    public static ChoiceQuestion<TEnum> Choice<TEnum>(Entry instructions)
        where TEnum : struct, Enum
        => new(instructions, ChoiceCriteria.FromEnum<TEnum>(descriptions: null));

    /// <summary>
    /// Creates a question whose labels are the members of <typeparamref name="TEnum"/>. A member's label is
    /// its name, or the value of <see cref="System.Runtime.Serialization.EnumMemberAttribute"/> when present.
    /// </summary>
    /// <typeparam name="TEnum">A non-flags enum type with at least one member.</typeparam>
    /// <param name="instructions">The question.</param>
    /// <param name="descriptions">Descriptions by member; missing members are described as <c>null</c>.</param>
    /// <returns>The question.</returns>
    /// <exception cref="ArgumentException">The enum is unsuitable, or a description key is not a defined member.</exception>
    public static ChoiceQuestion<TEnum> Choice<TEnum>(Entry instructions, IReadOnlyDictionary<TEnum, Entry> descriptions)
        where TEnum : struct, Enum
        => new(instructions, ChoiceCriteria.FromEnum(Guard.NotNull(descriptions)));

    /// <summary>Creates a question scored against an ordered rubric.</summary>
    /// <param name="instructions">The question.</param>
    /// <param name="criteria">Descriptions indexed by score from zero; at least two are required when sent.</param>
    /// <returns>The question.</returns>
    public static ScoreQuestion Score(Entry instructions, ScoreCriteria criteria)
        => new(instructions, Guard.NotNull(criteria));

    /// <summary>Creates a question scored against an ordered rubric.</summary>
    /// <param name="instructions">The question.</param>
    /// <param name="criteria">Descriptions indexed by score from zero; at least two are required when sent.</param>
    /// <returns>The question.</returns>
    public static ScoreQuestion Score(Entry instructions, params Entry[] criteria)
        => new(instructions, new ScoreCriteria(Guard.NotNull(criteria)));
}
