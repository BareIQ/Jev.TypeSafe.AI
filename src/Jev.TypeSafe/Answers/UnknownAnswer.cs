using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>An answer of a type this SDK version does not recognize. Inspect <see cref="Answer.Raw"/>.</summary>
public sealed class UnknownAnswer : Answer
{
    internal UnknownAnswer(JsonElement raw, string type)
        : base(raw)
        => Type = type;

    /// <inheritdoc/>
    public override string Type { get; }
}
