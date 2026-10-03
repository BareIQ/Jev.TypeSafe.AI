using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// A value accepted by the API for state, instructions, and criteria descriptions: text, a JSON
/// object, a JSON array, or <c>null</c>. The default value is <see cref="Omitted"/>, which leaves
/// optional properties out of the request entirely.
/// </summary>
/// <remarks>Entries are immutable snapshots; later changes to a source <see cref="JsonNode"/> have no effect.</remarks>
[DebuggerDisplay("{Kind}: {ToJsonString(),nq}")]
public readonly struct Entry : IEquatable<Entry>
{
    /// <summary>Text for <see cref="EntryKind.Text"/>; compact JSON for objects and arrays.</summary>
    private readonly string? _value;

    private Entry(EntryKind kind, string? value)
    {
        Kind = kind;
        _value = value;
    }

    /// <summary>Gets an entry that is left out of the request.</summary>
    public static Entry Omitted => default;

    /// <summary>Gets an entry that is sent as JSON <c>null</c>.</summary>
    public static Entry Null { get; } = new(EntryKind.Null, null);

    /// <summary>Gets the kind of value this entry holds.</summary>
    public EntryKind Kind { get; }

    /// <summary>Gets a value indicating whether this entry is <see cref="Omitted"/>.</summary>
    public bool IsOmitted => Kind == EntryKind.Omitted;

    /// <summary>Creates a text entry; <see langword="null"/> creates <see cref="Null"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The entry.</returns>
    public static Entry FromText(string? text) => text is null ? Null : new Entry(EntryKind.Text, text);

    /// <summary>Creates an entry from a JSON node: an object, an array, a string value, or <see langword="null"/>.</summary>
    /// <param name="node">The node. It is copied.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The node is a number or a boolean.</exception>
    public static Entry FromJson(JsonNode? node) => node switch
    {
        null => Null,
        JsonObject => new Entry(EntryKind.Object, JsonText.Compact(node)),
        JsonArray => new Entry(EntryKind.Array, JsonText.Compact(node)),
        JsonValue value when value.TryGetValue(out string? text) => FromText(text),
        _ => throw Unsupported(node.GetValueKind(), nameof(node)),
    };

    /// <summary>Creates an entry from a JSON element: an object, an array, a string, or <c>null</c>.</summary>
    /// <param name="element">The element. It is copied.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The element is a number, a boolean, or undefined.</exception>
    public static Entry FromJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => Null,
        JsonValueKind.String => FromText(element.GetString()),
        JsonValueKind.Object => new Entry(EntryKind.Object, JsonText.Compact(element)),
        JsonValueKind.Array => new Entry(EntryKind.Array, JsonText.Compact(element)),
        _ => throw Unsupported(element.ValueKind, nameof(element)),
    };

    /// <summary>Creates an entry by serializing a value with source-generated metadata.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">Serialization metadata for <typeparamref name="T"/>.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The value serializes to a number or a boolean.</exception>
    public static Entry FromObject<T>(T value, JsonTypeInfo<T> typeInfo)
        => FromJson(JsonSerializer.SerializeToElement(value, Guard.NotNull(typeInfo)));

    /// <summary>Creates an entry by serializing a value with reflection-based serialization and default options.</summary>
    /// <remarks>Not trim- or AOT-safe; prefer <see cref="FromObject{T}(T, JsonTypeInfo{T})"/> in trimmed applications.</remarks>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The value serializes to a number or a boolean.</exception>
    public static Entry FromObject<T>(T value) => FromObject(value, (JsonSerializerOptions?)null);

    /// <summary>Creates an entry by serializing a value with reflection-based serialization.</summary>
    /// <remarks>Not trim- or AOT-safe; prefer <see cref="FromObject{T}(T, JsonTypeInfo{T})"/> in trimmed applications.</remarks>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Serializer options, or <see langword="null"/> for defaults.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The value serializes to a number or a boolean.</exception>
    public static Entry FromObject<T>(T value, JsonSerializerOptions? options)
        => FromJson(JsonSerializer.SerializeToElement(value, options));

    /// <summary>Returns the text of a <see cref="EntryKind.Text"/> entry; otherwise <see langword="null"/>.</summary>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public string? AsText() => Kind == EntryKind.Text ? _value : null;

    /// <summary>Returns a new JSON node for this entry; <see langword="null"/> for null or omitted entries.</summary>
    /// <returns>A fresh node the caller may modify.</returns>
    public JsonNode? ToJsonNode() => Kind switch
    {
        EntryKind.Text => JsonValue.Create(_value),
        EntryKind.Object or EntryKind.Array when _value is not null => JsonNode.Parse(_value),
        _ => null,
    };

    /// <summary>Returns compact JSON for this entry, or an empty string when omitted.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJsonString() => Kind switch
    {
        EntryKind.Omitted => string.Empty,
        EntryKind.Null => "null",
        EntryKind.Text when _value is not null => JsonText.Quote(_value),
        _ => _value ?? string.Empty,
    };

    /// <inheritdoc cref="ToJsonString"/>
    public override string ToString() => ToJsonString();

    /// <summary>Converts text to an entry.</summary>
    /// <param name="text">The text; <see langword="null"/> converts to <see cref="Null"/>.</param>
    public static implicit operator Entry(string? text) => FromText(text);

    /// <summary>Converts a JSON object to an entry.</summary>
    /// <param name="value">The object; <see langword="null"/> converts to <see cref="Null"/>.</param>
    public static implicit operator Entry(JsonObject? value) => FromJson(value);

    /// <summary>Converts a JSON array to an entry.</summary>
    /// <param name="value">The array; <see langword="null"/> converts to <see cref="Null"/>.</param>
    public static implicit operator Entry(JsonArray? value) => FromJson(value);

    /// <summary>Determines whether two entries have the same kind and JSON representation.</summary>
    public static bool operator ==(Entry left, Entry right) => left.Equals(right);

    /// <summary>Determines whether two entries differ in kind or JSON representation.</summary>
    public static bool operator !=(Entry left, Entry right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(Entry other) => Kind == other.Kind && string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Entry other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => unchecked(((int)Kind * 397) ^ (_value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value)));

    /// <summary>Writes the entry; omitted entries are written as <c>null</c>.</summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        switch (Kind)
        {
            case EntryKind.Text when _value is not null:
                writer.WriteStringValue(_value);
                break;
            case EntryKind.Object or EntryKind.Array when _value is not null:
                writer.WriteRawValue(_value, skipInputValidation: true);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static ArgumentException Unsupported(JsonValueKind kind, string paramName)
        => new($"An entry must be text, a JSON object, a JSON array, or null; got {kind}.", paramName);
}
