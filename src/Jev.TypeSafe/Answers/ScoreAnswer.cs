using System.Collections.Generic;
using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>The answer to a score question.</summary>
public sealed class ScoreAnswer : Answer
{
    internal const string WireType = "score";

    internal ScoreAnswer(
        JsonElement raw,
        double score,
        double confidence,
        IReadOnlyDictionary<int, Entry> legend,
        IReadOnlyDictionary<int, double> probabilities)
        : base(raw)
    {
        Score = score;
        Confidence = confidence;
        Legend = legend;
        Probabilities = probabilities;
    }

    /// <inheritdoc/>
    public override string Type => WireType;

    /// <summary>Gets the expected score, which may fall between integer rubric levels.</summary>
    public double Score { get; }

    /// <summary>Gets the reported confidence in the score.</summary>
    public double Confidence { get; }

    /// <summary>Gets the rubric descriptions keyed by score.</summary>
    public IReadOnlyDictionary<int, Entry> Legend { get; }

    /// <summary>Gets probabilities keyed by score.</summary>
    public IReadOnlyDictionary<int, double> Probabilities { get; }
}
