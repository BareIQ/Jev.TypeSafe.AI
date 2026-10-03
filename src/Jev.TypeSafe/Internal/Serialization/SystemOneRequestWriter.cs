using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypeSafe.AI.Internal.Serialization;

/// <summary>
/// Writes <c>POST /v1/systemone</c> bodies by hand, so property order, omitted values, and explicit
/// <c>null</c>s are exact and no reflection is needed.
/// </summary>
internal static class SystemOneRequestWriter
{
    public static byte[] Write(SystemOneRequest request, string model)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, JsonText.WriterOptions))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("state");
            request.State.WriteTo(writer);
            WriteQuestions(writer, request.Questions);
            writer.WriteString("model", model);
            WriteAdditionalProperties(writer, request.AdditionalProperties);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteQuestions(Utf8JsonWriter writer, QuestionSet questions)
    {
        writer.WriteStartObject("questions");
        foreach (KeyValuePair<string, Question> question in questions)
        {
            writer.WritePropertyName(question.Key);
            WriteQuestion(writer, question.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteQuestion(Utf8JsonWriter writer, Question question)
    {
        writer.WriteStartObject();
        writer.WriteString("type", WireType(question.Type));
        WriteOptional(writer, "instructions", question.Instructions);
        switch (question)
        {
            case NoulQuestion noul when noul.Criteria is not null:
                writer.WriteStartObject("criteria");
                WriteOptional(writer, "true", noul.Criteria.True);
                WriteOptional(writer, "false", noul.Criteria.False);
                writer.WriteEndObject();
                break;
            case ChoiceQuestion choice:
                writer.WriteStartObject("criteria");
                foreach (KeyValuePair<string, Entry> label in choice.Criteria)
                {
                    writer.WritePropertyName(label.Key);
                    label.Value.WriteTo(writer);
                }

                writer.WriteEndObject();
                break;
            case ScoreQuestion score:
                writer.WriteStartArray("criteria");
                foreach (Entry description in score.Criteria)
                {
                    description.WriteTo(writer);
                }

                writer.WriteEndArray();
                break;
        }

        writer.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string propertyName, Entry entry)
    {
        if (!entry.IsOmitted)
        {
            writer.WritePropertyName(propertyName);
            entry.WriteTo(writer);
        }
    }

    private static void WriteAdditionalProperties(Utf8JsonWriter writer, IDictionary<string, JsonNode?> properties)
    {
        foreach (KeyValuePair<string, JsonNode?> property in properties)
        {
            writer.WritePropertyName(property.Key);
            if (property.Value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }
    }

    private static string WireType(QuestionType type) => type switch
    {
        QuestionType.Noul => NoulAnswer.WireType,
        QuestionType.Choice => ChoiceAnswer.WireType,
        QuestionType.Score => ScoreAnswer.WireType,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown question type."),
    };
}
