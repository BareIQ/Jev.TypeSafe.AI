using System;
using System.Collections.Generic;
using System.Linq;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Public;

public class QuestionBuilderTests
{
    [Fact]
    public void Noul_WithoutArguments_HasNullInstructionsAndNoCriteria()
    {
        NoulQuestion question = Question.Noul();

        Assert.Equal(QuestionType.Noul, question.Type);
        Assert.Equal(Entry.Null, question.Instructions);
        Assert.Null(question.Criteria);
    }

    [Fact]
    public void Noul_WithInstructionsAndCriteria_KeepsThem()
    {
        var criteria = new NoulCriteria { True = "yes means X" };

        NoulQuestion question = Question.Noul("Is it X?", criteria);

        Assert.Equal("Is it X?", question.Instructions.AsText());
        Assert.Same(criteria, question.Criteria);
        Assert.False(criteria.True.IsOmitted);
        Assert.True(criteria.False.IsOmitted);
    }

    [Fact]
    public void Noul_ExplicitNullInstructions_StayNull()
    {
        Assert.Equal(Entry.Null, Question.Noul(Entry.Null).Instructions);
    }

    [Fact]
    public void Choice_CopiesItsCriteria()
    {
        var criteria = new ChoiceCriteria { { "a", "first" } };

        ChoiceQuestion question = Question.Choice("Pick", criteria);
        criteria.Add("b", "second");

        Assert.Equal(QuestionType.Choice, question.Type);
        Assert.Single(question.Criteria);
        Assert.Equal("first", question.Criteria.Single().Value.AsText());
    }

    [Fact]
    public void Choice_CriteriaOfAQuestion_AreReadOnly()
    {
        ChoiceQuestion question = Question.Choice("Pick", ChoiceCriteria.FromLabels("a"));

        Assert.Throws<InvalidOperationException>(() => question.Criteria.Add("b", Entry.Null));
    }

    [Fact]
    public void Choice_NullCriteria_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Question.Choice("x", (ChoiceCriteria)null!));
    }

    [Fact]
    public void Choice_Enum_UsesMemberNamesAndEnumMemberOverrides()
    {
        ChoiceQuestion<Tone> question = Question.Choice<Tone>("Tone?");

        Assert.Equal(QuestionType.Choice, question.Type);
        Assert.Equal(["Calm", "frustrated", "Angry"], question.Criteria.Select(pair => pair.Key));
        Assert.All(question.Criteria, pair => Assert.Equal(Entry.Null, pair.Value));
    }

    [Fact]
    public void Choice_EnumWithDescriptions_DescribesOnlyThoseMembers()
    {
        var descriptions = new Dictionary<Tone, Entry> { [Tone.Frustrated] = "annoyed", [Tone.Angry] = Entry.FromText("furious") };

        ChoiceQuestion<Tone> question = Question.Choice("Tone?", descriptions);

        Assert.Equal(Entry.Null, question.Criteria.First(pair => pair.Key == "Calm").Value);
        Assert.Equal("annoyed", question.Criteria.First(pair => pair.Key == "frustrated").Value.AsText());
        Assert.Equal("furious", question.Criteria.First(pair => pair.Key == "Angry").Value.AsText());
    }

    [Fact]
    public void Choice_EnumDescriptionForUndefinedMember_Throws()
    {
        var descriptions = new Dictionary<Tone, Entry> { [(Tone)99] = "nope" };

        Assert.Throws<ArgumentException>(() => Question.Choice("Tone?", descriptions));
    }

    [Fact]
    public void Choice_EnumDescriptionsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Question.Choice<Tone>("Tone?", null!));
    }

    [Fact]
    public void Choice_FlagsEnum_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Question.Choice<Permissions>("?"));

        Assert.Contains("flags", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Choice_EnumWithoutMembers_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Question.Choice<NoMembers>("?"));
    }

    [Fact]
    public void Choice_EnumMappingTwoMembersToOneLabel_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Question.Choice<DuplicateLabels>("?"));

        Assert.Contains("'First'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_ParamsAndCriteriaOverloads_BuildTheSameRubric()
    {
        ScoreQuestion fromParams = Question.Score("?", "low", "high");
        ScoreQuestion fromCriteria = Question.Score("?", ScoreCriteria.FromTexts("low", "high"));

        Assert.Equal(QuestionType.Score, fromParams.Type);
        Assert.Equal(fromParams.Criteria, fromCriteria.Criteria);
    }

    [Fact]
    public void Score_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Question.Score("?", (ScoreCriteria)null!));
        Assert.Throws<ArgumentNullException>(() => Question.Score("?", (Entry[])null!));
    }
}

public class ChoiceCriteriaTests
{
    [Fact]
    public void CollectionInitializer_BuildsAnOrderedMap()
    {
        var criteria = new ChoiceCriteria { { "billing", "Payments" }, { "other", Entry.Null } };

        Assert.Equal(2, criteria.Count);
        Assert.Equal("billing", criteria[0].Key);
        Assert.Equal("Payments", criteria[0].Value.AsText());
        Assert.Equal("other", criteria[1].Key);
    }

    [Fact]
    public void FromLabels_DescribesEveryLabelAsNull()
    {
        ChoiceCriteria criteria = ChoiceCriteria.FromLabels("a", "b", "c");

        Assert.Equal(["a", "b", "c"], criteria.Select(pair => pair.Key));
        Assert.All(criteria, pair => Assert.Equal(Entry.Null, pair.Value));
    }

    [Fact]
    public void Add_DuplicateLabel_Throws()
    {
        var criteria = new ChoiceCriteria { { "a", "x" } };

        var exception = Assert.Throws<ArgumentException>(() => criteria.Add("a", "y"));

        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromLabels_DuplicateLabel_Throws()
    {
        Assert.Throws<ArgumentException>(() => ChoiceCriteria.FromLabels("a", "a"));
    }

    [Fact]
    public void Add_NullLabel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ChoiceCriteria().Add(null!, Entry.Null));
        Assert.Throws<ArgumentNullException>(() => ChoiceCriteria.FromLabels(null!));
    }

    [Fact]
    public void Labels_AreCaseSensitiveAndMayBeAnyString()
    {
        var criteria = new ChoiceCriteria { { "A", "1" }, { "a", "2" }, { "", "3" }, { "__proto__", "4" } };

        Assert.Equal(4, criteria.Count);
    }

    [Fact]
    public void TryGetDescription_FindsExistingLabelsOnly()
    {
        var criteria = new ChoiceCriteria { { "a", "described" } };

        Assert.True(criteria.TryGetDescription("a", out Entry found));
        Assert.Equal("described", found.AsText());
        Assert.False(criteria.TryGetDescription("zzz", out Entry missing));
        Assert.True(missing.IsOmitted);
        Assert.Throws<ArgumentNullException>(() => criteria.TryGetDescription(null!, out _));
    }

    [Fact]
    public void Enumeration_FollowsInsertionOrder()
    {
        System.Collections.IEnumerable criteria = new ChoiceCriteria { { "z", "1" }, { "a", "2" } };

        Assert.Equal(["z", "a"], criteria.Cast<KeyValuePair<string, Entry>>().Select(pair => pair.Key));
    }
}

public class ScoreCriteriaTests
{
    [Fact]
    public void Constructor_CopiesTheDescriptionsInOrder()
    {
        var source = new List<Entry> { "low", Entry.Null, "high" };

        var criteria = new ScoreCriteria(source);
        source.Add("extra");

        Assert.Equal(3, criteria.Count);
        Assert.Equal("low", criteria[0].AsText());
        Assert.Equal(Entry.Null, criteria[1]);
        Assert.Equal("high", criteria[2].AsText());
        Assert.Equal(3, criteria.ToArray().Length);
    }

    [Fact]
    public void ImplicitConversions_AcceptStringAndEntryArrays()
    {
        ScoreCriteria fromStrings = new[] { "a", "b" };
        ScoreCriteria fromEntries = new Entry[] { "a", "b" };
        ScoreCriteria fromNulls = new Entry[] { Entry.Null, Entry.Null };

        Assert.Equal(fromStrings, fromEntries);
        Assert.Equal(2, fromNulls.Count);
    }

    [Fact]
    public void FromTexts_NullEntriesBecomeNullDescriptions()
    {
        ScoreCriteria criteria = ScoreCriteria.FromTexts("a", null!);

        Assert.Equal(Entry.Null, criteria[1]);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ScoreCriteria(null!));
        Assert.Throws<ArgumentNullException>(() => ScoreCriteria.FromEntries(null!));
        Assert.Throws<ArgumentNullException>(() => ScoreCriteria.FromTexts(null!));
    }
}

public class QuestionSetTests
{
    [Fact]
    public void Add_ReturnsKeysCarryingTheNameAndEnumerationFollowsInsertionOrder()
    {
        var set = new QuestionSet();

        QuestionKey<NoulAnswer> a = set.Add("a", Question.Noul());
        QuestionKey<ChoiceAnswer> b = set.Add("b", Question.Choice("?", ChoiceCriteria.FromLabels("x")));
        QuestionKey<ChoiceAnswer<Tone>> c = set.Add("c", Question.Choice<Tone>("?"));
        QuestionKey<ScoreAnswer> d = set.Add("d", Question.Score("?", "lo", "hi"));

        Assert.Equal(["a", "b", "c", "d"], [a.Name, b.Name, c.Name, d.Name]);
        Assert.Equal(["a", "b", "c", "d"], set.Select(pair => pair.Key));
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void Add_DuplicateName_Throws()
    {
        var set = new QuestionSet();
        set.Add("a", Question.Noul());

        var exception = Assert.Throws<ArgumentException>(() => set.Add("a", Question.Score("?", "x", "y")));

        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_AnyStringIsAValidName()
    {
        var set = new QuestionSet();

        set.Add("", Question.Noul());
        set.Add("__proto__", Question.Noul());
        set.Add("Hello \"World\" 你好", Question.Noul());
        set.Add("A", Question.Noul());
        set.Add("a", Question.Noul());

        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void Add_NullArguments_Throw()
    {
        var set = new QuestionSet();

        Assert.Throws<ArgumentNullException>(() => set.Add(null!, Question.Noul()));
        Assert.Throws<ArgumentNullException>(() => set.Add("a", (NoulQuestion)null!));
    }

    [Fact]
    public void ContainsAndTryGetQuestion_LookUpByName()
    {
        var set = new QuestionSet();
        NoulQuestion question = Question.Noul("?");
        set.Add("a", question);

        Assert.True(set.Contains("a"));
        Assert.False(set.Contains("A"));
        Assert.True(set.TryGetQuestion("a", out Question? found));
        Assert.Same(question, found);
        Assert.False(set.TryGetQuestion("missing", out Question? missing));
        Assert.Null(missing);
        Assert.Throws<ArgumentNullException>(() => set.Contains(null!));
    }

    [Fact]
    public void NonGenericEnumeration_Works()
    {
        var set = new QuestionSet();
        set.Add("a", Question.Noul());

        Assert.Single(((System.Collections.IEnumerable)set).Cast<object>());
    }
}

public class QuestionKeyTests
{
    [Fact]
    public void Keys_CompareByName()
    {
        var first = new QuestionSet().Add("same", Question.Noul());
        var second = new QuestionSet().Add("same", Question.Noul());
        var other = new QuestionSet().Add("other", Question.Noul());

        Assert.True(first == second);
        Assert.True(first != other);
        Assert.True(first.Equals((object)second));
        Assert.False(first.Equals(new object()));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal("same", first.ToString());
    }

    [Fact]
    public void DefaultKey_HasAnEmptyName()
    {
        QuestionKey<NoulAnswer> key = default;

        Assert.Equal(string.Empty, key.Name);
    }
}
