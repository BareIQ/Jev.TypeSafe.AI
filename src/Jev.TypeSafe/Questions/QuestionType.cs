namespace TypeSafe.AI;

/// <summary>The kind of a question.</summary>
public enum QuestionType
{
    /// <summary>A yes/no question answered with a probability (<c>"noul"</c>).</summary>
    Noul = 0,

    /// <summary>A question that selects between named labels (<c>"choice"</c>).</summary>
    Choice,

    /// <summary>A question scored against an ordered rubric (<c>"score"</c>).</summary>
    Score,
}
