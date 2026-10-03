using System.Collections.Generic;
using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>The answer to a choice question.</summary>
/// <remarks>The type is not sealed only so that <see cref="ChoiceAnswer{TEnum}"/> can extend it; it has no public constructor.</remarks>
public class ChoiceAnswer : Answer
{
    internal const string WireType = "choice";

    internal ChoiceAnswer(JsonElement raw, string choice, double confidence, IReadOnlyDictionary<string, double> probabilities)
        : base(raw)
    {
        Choice = choice;
        Confidence = confidence;
        Probabilities = probabilities;
    }

    /// <inheritdoc/>
    public override string Type => WireType;

    /// <summary>Gets the selected label.</summary>
    public string Choice { get; }

    /// <summary>Gets the reported confidence in the selected label.</summary>
    public double Confidence { get; }

    /// <summary>Gets probabilities keyed by label, in server order.</summary>
    public IReadOnlyDictionary<string, double> Probabilities { get; }
}
