using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>An answer to one question. Use the derived types for typed values.</summary>
public abstract class Answer
{
    private protected Answer(JsonElement raw) => Raw = raw;

    /// <summary>Gets the answer type as sent by the server, for example <c>"noul"</c>.</summary>
    public abstract string Type { get; }

    /// <summary>Gets the complete answer JSON, including any fields this SDK does not model.</summary>
    public JsonElement Raw { get; }
}
