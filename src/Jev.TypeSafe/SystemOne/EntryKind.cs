namespace TypeSafe.AI;

/// <summary>The kind of value held by an <see cref="Entry"/>.</summary>
public enum EntryKind
{
    /// <summary>No value; the property is left out of the request.</summary>
    Omitted = 0,

    /// <summary>An explicit JSON <c>null</c>.</summary>
    Null,

    /// <summary>A text value.</summary>
    Text,

#pragma warning disable CA1720 // Mirrors System.Text.Json.JsonValueKind, which names these JSON kinds the same way.
    /// <summary>A JSON object.</summary>
    Object,

    /// <summary>A JSON array.</summary>
    Array,
#pragma warning restore CA1720
}
