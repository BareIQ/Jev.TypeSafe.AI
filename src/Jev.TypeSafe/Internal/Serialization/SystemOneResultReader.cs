using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Http;

namespace TypeSafe.AI.Internal.Serialization;

/// <summary>Reads <c>POST /v1/systemone</c> responses into result models.</summary>
internal static class SystemOneResultReader
{
    private static readonly JsonShapeReader s_shape = new("POST " + ApiPaths.SystemOne);

    /// <exception cref="TypeSafeException">The response does not have the documented shape.</exception>
    public static SystemOneResult Read(JsonElement? body)
    {
        JsonElement root = body ?? throw s_shape.Unexpected("a JSON object");
        s_shape.RequireObject(root, "$");
        string model = s_shape.RequireString(root, "model", string.Empty);
        JsonElement answers = s_shape.RequireProperty(root, "answers", JsonValueKind.Object, string.Empty);
        JsonElement usage = s_shape.RequireProperty(root, "usage", JsonValueKind.Object, string.Empty);
        return new SystemOneResult(
            model,
            ReadAnswers(answers),
            new Usage(s_shape.RequireInt64(usage, "input_tokens", "usage"), s_shape.RequireInt64(usage, "output_tokens", "usage")));
    }

    /// <summary>Reads one answer; unrecognized types become <see cref="UnknownAnswer"/>.</summary>
    public static Answer ReadAnswer(JsonElement answer, string path)
    {
        s_shape.RequireObject(answer, path);
        string type = answer.TryGetProperty("type", out JsonElement typeElement) && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;
        return type switch
        {
            NoulAnswer.WireType => new NoulAnswer(answer, s_shape.RequireDouble(answer, "noul", path)),
            ChoiceAnswer.WireType => ReadChoice(answer, path),
            ScoreAnswer.WireType => ReadScore(answer, path),
            _ => new UnknownAnswer(answer, type),
        };
    }

    private static AnswerSet ReadAnswers(JsonElement answers)
    {
        var items = new List<KeyValuePair<string, Answer>>();
        foreach (JsonProperty property in answers.EnumerateObject())
        {
            items.Add(new KeyValuePair<string, Answer>(property.Name, ReadAnswer(property.Value, $"answers.{property.Name}")));
        }

        return new AnswerSet(new OrderedReadOnlyDictionary<string, Answer>(items, System.StringComparer.Ordinal));
    }

    private static ChoiceAnswer ReadChoice(JsonElement answer, string path)
    {
        JsonElement probabilities = s_shape.RequireProperty(answer, "probabilities", JsonValueKind.Object, path);
        var byLabel = new List<KeyValuePair<string, double>>();
        foreach (JsonProperty probability in probabilities.EnumerateObject())
        {
            byLabel.Add(new(probability.Name, s_shape.RequireDouble(probability.Value, $"{path}.probabilities.{probability.Name}")));
        }

        return new ChoiceAnswer(
            answer,
            s_shape.RequireString(answer, "choice", path),
            s_shape.RequireDouble(answer, "confidence", path),
            new OrderedReadOnlyDictionary<string, double>(byLabel, System.StringComparer.Ordinal));
    }

    private static ScoreAnswer ReadScore(JsonElement answer, string path)
    {
        JsonElement legend = s_shape.RequireProperty(answer, "legend", JsonValueKind.Object, path);
        JsonElement probabilities = s_shape.RequireProperty(answer, "probabilities", JsonValueKind.Object, path);
        var legendByScore = new List<KeyValuePair<int, Entry>>();
        foreach (JsonProperty level in legend.EnumerateObject())
        {
            legendByScore.Add(new(ParseScore(level.Name, $"{path}.legend"), ReadLegendEntry(level.Value, $"{path}.legend.{level.Name}")));
        }

        var probabilityByScore = new List<KeyValuePair<int, double>>();
        foreach (JsonProperty probability in probabilities.EnumerateObject())
        {
            string probabilityPath = $"{path}.probabilities.{probability.Name}";
            probabilityByScore.Add(new(ParseScore(probability.Name, $"{path}.probabilities"), s_shape.RequireDouble(probability.Value, probabilityPath)));
        }

        return new ScoreAnswer(
            answer,
            s_shape.RequireDouble(answer, "score", path),
            s_shape.RequireDouble(answer, "confidence", path),
            new OrderedReadOnlyDictionary<int, Entry>(legendByScore),
            new OrderedReadOnlyDictionary<int, double>(probabilityByScore));
    }

    private static int ParseScore(string key, string path)
        => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int score)
            ? score
            : throw s_shape.Unexpected($"integer score keys at '{path}', got '{key}'");

    private static Entry ReadLegendEntry(JsonElement value, string path)
        => value.ValueKind is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null
            ? Entry.FromJson(value)
            : throw s_shape.Unexpected($"text, an object, an array, or null at '{path}'");
}
