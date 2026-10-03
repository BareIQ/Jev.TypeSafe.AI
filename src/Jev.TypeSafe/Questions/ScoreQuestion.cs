namespace TypeSafe.AI;

/// <summary>A question scored against an ordered rubric; the answer is an expected score.</summary>
public sealed class ScoreQuestion : Question
{
    internal ScoreQuestion(Entry instructions, ScoreCriteria criteria)
        : base(instructions)
        => Criteria = criteria;

    /// <inheritdoc/>
    public override QuestionType Type => QuestionType.Score;

    /// <summary>Gets the rubric descriptions, indexed by score from zero.</summary>
    public ScoreCriteria Criteria { get; }
}
