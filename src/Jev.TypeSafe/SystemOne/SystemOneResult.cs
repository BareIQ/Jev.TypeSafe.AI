namespace TypeSafe.AI;

/// <summary>Answers keyed by question name, with model and usage metadata.</summary>
public sealed class SystemOneResult
{
    internal SystemOneResult(string model, AnswerSet answers, Usage usage)
    {
        Model = model;
        Answers = answers;
        Usage = usage;
    }

    /// <summary>Gets the model that answered the request.</summary>
    public string Model { get; }

    /// <summary>Gets the answers keyed by question name.</summary>
    public AnswerSet Answers { get; }

    /// <summary>Gets token usage for the request.</summary>
    public Usage Usage { get; }
}
