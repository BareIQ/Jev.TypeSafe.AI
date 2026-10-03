namespace TypeSafe.AI;

/// <summary>A question that selects between named labels.</summary>
/// <remarks>The type is not sealed only so that <see cref="ChoiceQuestion{TEnum}"/> can extend it; it has no public constructor.</remarks>
public class ChoiceQuestion : Question
{
    internal ChoiceQuestion(Entry instructions, ChoiceCriteria criteria)
        : base(instructions)
        => Criteria = criteria;

    /// <inheritdoc/>
    public override QuestionType Type => QuestionType.Choice;

    /// <summary>Gets the labels and their descriptions, in the order they are sent.</summary>
    public ChoiceCriteria Criteria { get; }
}
