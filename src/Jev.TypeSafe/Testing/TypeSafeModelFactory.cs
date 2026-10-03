using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI.Testing;

/// <summary>
/// Creates result models for tests of code that consumes <see cref="ITypeSafeClient"/>. Models are built from the
/// same JSON the server would send, so their <see cref="Answer.Raw"/> values are realistic.
/// </summary>
public static class TypeSafeModelFactory
{
    /// <summary>Creates a yes/no answer.</summary>
    /// <param name="noul">The probability of yes.</param>
    /// <returns>The answer.</returns>
    public static NoulAnswer NoulAnswer(double noul)
        => (NoulAnswer)ReadAnswer(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", AI.NoulAnswer.WireType);
            writer.WriteNumber("noul", noul);
            writer.WriteEndObject();
        });

    /// <summary>Creates a choice answer.</summary>
    /// <param name="choice">The selected label.</param>
    /// <param name="confidence">The confidence in the selected label.</param>
    /// <param name="probabilities">Probabilities by label.</param>
    /// <returns>The answer.</returns>
    public static ChoiceAnswer ChoiceAnswer(string choice, double confidence, IEnumerable<KeyValuePair<string, double>> probabilities)
    {
        Guard.NotNull(choice);
        Guard.NotNull(probabilities);
        return (ChoiceAnswer)ReadAnswer(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", AI.ChoiceAnswer.WireType);
            writer.WriteString("choice", choice);
            writer.WriteNumber("confidence", confidence);
            writer.WriteStartObject("probabilities");
            foreach (KeyValuePair<string, double> probability in probabilities)
            {
                writer.WriteNumber(probability.Key, probability.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>Creates a score answer.</summary>
    /// <param name="score">The expected score.</param>
    /// <param name="confidence">The confidence in the score.</param>
    /// <param name="legend">Rubric descriptions by score.</param>
    /// <param name="probabilities">Probabilities by score.</param>
    /// <returns>The answer.</returns>
    public static ScoreAnswer ScoreAnswer(
        double score,
        double confidence,
        IEnumerable<KeyValuePair<int, Entry>> legend,
        IEnumerable<KeyValuePair<int, double>> probabilities)
    {
        Guard.NotNull(legend);
        Guard.NotNull(probabilities);
        return (ScoreAnswer)ReadAnswer(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", AI.ScoreAnswer.WireType);
            writer.WriteNumber("score", score);
            writer.WriteNumber("confidence", confidence);
            writer.WriteStartObject("legend");
            foreach (KeyValuePair<int, Entry> level in legend)
            {
                writer.WritePropertyName(ScoreKey(level.Key));
                level.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteStartObject("probabilities");
            foreach (KeyValuePair<int, double> probability in probabilities)
            {
                writer.WriteNumber(ScoreKey(probability.Key), probability.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>Creates a result.</summary>
    /// <param name="model">The model name.</param>
    /// <param name="answers">Answers by question name.</param>
    /// <param name="inputTokens">The input tokens used.</param>
    /// <param name="outputTokens">The output tokens used.</param>
    /// <returns>The result.</returns>
    public static SystemOneResult SystemOneResult(
        string model,
        IEnumerable<KeyValuePair<string, Answer>> answers,
        long inputTokens = 0,
        long outputTokens = 0)
        => new(
            Guard.NotNull(model),
            new AnswerSet(new OrderedReadOnlyDictionary<string, Answer>(Guard.NotNull(answers), StringComparer.Ordinal)),
            new Usage(inputTokens, outputTokens));

    /// <summary>Creates a model card.</summary>
    /// <param name="name">The model name.</param>
    /// <param name="description">The description.</param>
    /// <param name="releaseDate">The release date text.</param>
    /// <param name="additionalProperties">Other fields, if any.</param>
    /// <returns>The model card.</returns>
    public static ModelCard ModelCard(
        string name,
        string description,
        string releaseDate,
        IEnumerable<KeyValuePair<string, JsonElement>>? additionalProperties = null)
        => new(
            Guard.NotNull(name),
            Guard.NotNull(description),
            Guard.NotNull(releaseDate),
            new OrderedReadOnlyDictionary<string, JsonElement>(additionalProperties ?? [], StringComparer.Ordinal));

    /// <summary>Creates a buffered HTTP response.</summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="content">The body text, or <see langword="null"/> for an empty body.</param>
    /// <param name="headers">Headers, if any.</param>
    /// <returns>The response.</returns>
    public static RawResponse RawResponse(int statusCode, string? content = null, IEnumerable<KeyValuePair<string, string>>? headers = null)
    {
        var grouped = new List<KeyValuePair<string, IEnumerable<string>>>();
        foreach (KeyValuePair<string, string> header in headers ?? [])
        {
            grouped.Add(new(header.Key, [header.Value]));
        }

        return RawResponseFactory.Create(statusCode, grouped, content is null ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(content));
    }

    /// <summary>Creates a result paired with its HTTP response.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="value">The result.</param>
    /// <param name="response">The response; defaults to an empty 200 response.</param>
    /// <returns>The pair.</returns>
    public static ApiResponse<T> ApiResponse<T>(T value, RawResponse? response = null)
        => new(value, response ?? RawResponse(200));

    private static Answer ReadAnswer(Action<Utf8JsonWriter> write)
        => SystemOneResultReader.ReadAnswer(JsonText.ParseElement(JsonText.Build(write)), "answer");

    private static string ScoreKey(int score) => score.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
