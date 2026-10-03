using System;
using System.Collections.Generic;

namespace TypeSafe.AI.Internal.Validation;

/// <summary>Rejects requests the API would reject, before anything is sent.</summary>
internal static class QuestionValidator
{
    private static readonly string[] s_reservedProperties = ["state", "questions", "model"];

    /// <exception cref="ArgumentException">An additional property uses a reserved name.</exception>
    /// <exception cref="TypeSafeException">There are no questions, or a score question has fewer than two criteria.</exception>
    public static void Validate(SystemOneRequest request)
    {
        foreach (string reserved in s_reservedProperties)
        {
            if (request.AdditionalProperties.ContainsKey(reserved))
            {
                throw new ArgumentException(
                    $"'{reserved}' cannot be set through AdditionalProperties; use the SystemOneRequest property instead.",
                    nameof(request));
            }
        }

        if (request.Questions.Count == 0)
        {
            throw new TypeSafeException("At least one question is required.");
        }

        foreach (KeyValuePair<string, Question> question in request.Questions)
        {
            if (question.Value is ScoreQuestion { Criteria.Count: < 2 } score)
            {
                throw new TypeSafeException(
                    $"Score question \"{question.Key}\" has {score.Criteria.Count} criteria; at least two scores are required.");
            }
        }
    }
}
