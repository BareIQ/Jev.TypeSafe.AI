using System;

namespace TypeSafe.AI;

/// <summary>A choice question whose labels are the members of <typeparamref name="TEnum"/>.</summary>
/// <typeparam name="TEnum">The enum that defines the labels.</typeparam>
public sealed class ChoiceQuestion<TEnum> : ChoiceQuestion
    where TEnum : struct, Enum
{
    internal ChoiceQuestion(Entry instructions, ChoiceCriteria criteria)
        : base(instructions, criteria)
    {
    }
}
