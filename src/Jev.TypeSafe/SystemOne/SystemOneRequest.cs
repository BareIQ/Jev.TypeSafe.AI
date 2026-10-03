using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>State and named questions for <see cref="ITypeSafeClient.SystemOneAsync"/>.</summary>
public sealed class SystemOneRequest
{
    /// <summary>Initializes a new instance of the <see cref="SystemOneRequest"/> class.</summary>
    /// <param name="state">Text, a JSON object or array, or <see cref="Entry.Null"/>, to evaluate.</param>
    /// <param name="questions">The questions; at least one is required when sent.</param>
    /// <exception cref="ArgumentException"><paramref name="state"/> is <see cref="Entry.Omitted"/>.</exception>
    public SystemOneRequest(Entry state, QuestionSet questions)
    {
        if (state.IsOmitted)
        {
            throw new ArgumentException("State is required; use Entry.Null to send null.", nameof(state));
        }

        State = state;
        Questions = Guard.NotNull(questions);
    }

    /// <summary>Gets the state to evaluate.</summary>
    public Entry State { get; }

    /// <summary>Gets the questions, keyed by the names that identify their answers.</summary>
    public QuestionSet Questions { get; }

    /// <summary>Gets the model override; <see langword="null"/> uses the client's default model.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets additional top-level request properties, forwarded verbatim (including <see langword="null"/> values).
    /// The names <c>state</c>, <c>questions</c>, and <c>model</c> are reserved.
    /// </summary>
    public IDictionary<string, JsonNode?> AdditionalProperties { get; } = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
}
