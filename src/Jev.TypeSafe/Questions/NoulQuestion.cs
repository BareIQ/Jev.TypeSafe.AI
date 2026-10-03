namespace TypeSafe.AI;

/// <summary>A yes/no question, answered with the probability of yes.</summary>
public sealed class NoulQuestion : Question
{
    internal NoulQuestion(Entry instructions, NoulCriteria? criteria)
        : base(instructions)
        => Criteria = criteria;

    /// <inheritdoc/>
    public override QuestionType Type => QuestionType.Noul;

    /// <summary>Gets the optional outcome descriptions; <see langword="null"/> when criteria are left out.</summary>
    public NoulCriteria? Criteria { get; }
}
