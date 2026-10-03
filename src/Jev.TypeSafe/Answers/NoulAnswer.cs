using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>The answer to a yes/no question.</summary>
public sealed class NoulAnswer : Answer
{
    internal const string WireType = "noul";

    internal NoulAnswer(JsonElement raw, double noul)
        : base(raw)
        => Noul = noul;

    /// <inheritdoc/>
    public override string Type => WireType;

    /// <summary>Gets the probability of a yes answer, from zero to one.</summary>
    public double Noul { get; }
}
