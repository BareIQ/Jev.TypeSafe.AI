using System;

namespace TypeSafe.AI;

/// <summary>Converts untyped answers to the type a question key expects.</summary>
internal static class AnswerConversion
{
    public static TAnswer As<TAnswer>(Answer answer, string questionName)
        where TAnswer : Answer
        => answer as TAnswer ?? throw new InvalidOperationException(
            $"The answer to '{questionName}' is a '{answer.Type}' answer and cannot be read as {typeof(TAnswer).Name}.");

    public static ChoiceAnswer<TEnum> AsEnumChoice<TEnum>(Answer answer, string questionName)
        where TEnum : struct, Enum
        => ChoiceAnswer<TEnum>.From(As<ChoiceAnswer>(answer, questionName), questionName);
}
