using System;
using System.Collections.Generic;
using System.Text.Json;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Http;

namespace TypeSafe.AI.Internal.Serialization;

/// <summary>Reads <c>GET /v1/models</c> responses, which wrap the list as <c>{ "models": [...] }</c>.</summary>
internal static class ModelsReader
{
    private static readonly JsonShapeReader s_shape = new("GET " + ApiPaths.Models);

    /// <exception cref="TypeSafeException">The response does not have the documented shape.</exception>
    public static IReadOnlyList<ModelCard> Read(JsonElement? root)
    {
        if (root is not { ValueKind: JsonValueKind.Object } envelope
            || !envelope.TryGetProperty("models", out JsonElement models)
            || models.ValueKind != JsonValueKind.Array)
        {
            throw s_shape.Unexpected("{ models: [...] }");
        }

        var cards = new List<ModelCard>(models.GetArrayLength());
        int index = 0;
        foreach (JsonElement card in models.EnumerateArray())
        {
            cards.Add(ReadCard(card, $"models.{index++}"));
        }

        return cards.AsReadOnly();
    }

    private static ModelCard ReadCard(JsonElement card, string path)
    {
        s_shape.RequireObject(card, path);
        var additional = new List<KeyValuePair<string, JsonElement>>();
        foreach (JsonProperty property in card.EnumerateObject())
        {
            if (property.Name is not ("name" or "description" or "release_date"))
            {
                additional.Add(new(property.Name, property.Value));
            }
        }

        return new ModelCard(
            s_shape.RequireString(card, "name", path),
            s_shape.RequireString(card, "description", path),
            s_shape.RequireString(card, "release_date", path),
            new OrderedReadOnlyDictionary<string, JsonElement>(additional, StringComparer.Ordinal));
    }
}
