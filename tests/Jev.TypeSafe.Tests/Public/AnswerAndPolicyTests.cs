using System;
using System.Collections.Generic;
using System.Linq;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Public;

public class AnswerSetTests
{
    private const string Response = """
        {"model":"m","answers":{
          "billing":{"type":"noul","noul":0.9},
          "tone":{"type":"choice","choice":"frustrated","confidence":0.7,"probabilities":{"Calm":0.1,"frustrated":0.7,"Angry":0.2}},
          "category":{"type":"choice","choice":"billing","confidence":0.8,"probabilities":{"billing":0.8,"other":0.2}},
          "badTone":{"type":"choice","choice":"sleepy","confidence":0.8,"probabilities":{"sleepy":1}},
          "urgency":{"type":"score","score":1.5,"confidence":0.5,"legend":{"0":"a","1":"b"},"probabilities":{"0":0.5,"1":0.5}}
        },"usage":{"input_tokens":1,"output_tokens":1}}
        """;

    private static AnswerSet Answers { get; } = SystemOneResultReader.Read(JsonText.ParseElement(Response)).Answers;

    [Fact]
    public void Get_TypedKeys_ReturnTheMatchingAnswerTypes()
    {
        var set = new QuestionSet();
        var billing = set.Add("billing", Question.Noul());
        var category = set.Add("category", Question.Choice("?", ChoiceCriteria.FromLabels("billing", "other")));
        var urgency = set.Add("urgency", Question.Score("?", "a", "b"));

        Assert.Equal(0.9, Answers.Get(billing).Noul);
        Assert.Equal("billing", Answers.Get(category).Choice);
        Assert.Equal(1.5, Answers.Get(urgency).Score);
    }

    [Fact]
    public void Get_EnumKey_MapsLabelsToEnumValues()
    {
        var tone = new QuestionSet().Add("tone", Question.Choice<Tone>("?"));

        ChoiceAnswer<Tone> answer = Answers.Get(tone);

        Assert.Equal(Tone.Frustrated, answer.Value);
        Assert.Equal("frustrated", answer.Choice);
        Assert.Equal(0.7, answer.Confidence);
        Assert.Equal([Tone.Calm, Tone.Frustrated, Tone.Angry], answer.ProbabilitiesByValue.Keys);
        Assert.Equal(0.2, answer.ProbabilitiesByValue[Tone.Angry]);
        Assert.Equal(0.2, answer.Probabilities["Angry"]);
    }

    [Fact]
    public void GetChoice_Generic_MapsLabelsToEnumValuesAndIsRepeatable()
    {
        ChoiceAnswer<Tone> first = Answers.GetChoice<Tone>("tone");
        ChoiceAnswer<Tone> second = Answers.GetChoice<Tone>("tone");

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void GetChoice_EnumLabelThatDoesNotMatch_ThrowsTypeSafeException()
    {
        var exception = Assert.Throws<TypeSafeException>(() => Answers.GetChoice<Tone>("badTone"));

        Assert.Equal("The label 'sleepy' in the answer to 'badTone' does not match any member of Tone.", exception.Message);
    }

    [Fact]
    public void Get_AnswerOfAnotherKind_ThrowsInvalidOperationNamingBoth()
    {
        var key = new QuestionSet().Add("category", Question.Noul());

        var exception = Assert.Throws<InvalidOperationException>(() => Answers.Get(key));

        Assert.Equal("The answer to 'category' is a 'choice' answer and cannot be read as NoulAnswer.", exception.Message);
    }

    [Fact]
    public void NamedGetters_ReturnTypedAnswersOrThrow()
    {
        Assert.Equal(0.9, Answers.GetNoul("billing").Noul);
        Assert.Equal("billing", Answers.GetChoice("category").Choice);
        Assert.Equal(1.5, Answers.GetScore("urgency").Score);
        Assert.Throws<InvalidOperationException>(() => Answers.GetScore("billing"));
        Assert.Throws<InvalidOperationException>(() => Answers.GetChoice<Tone>("billing"));
        Assert.Throws<KeyNotFoundException>(() => Answers.GetNoul("missing"));
    }

    [Fact]
    public void TryGet_FindsPresentAnswersAndReportsMissingOnes()
    {
        var present = new QuestionSet().Add("billing", Question.Noul());
        var missing = new QuestionSet().Add("nope", Question.Noul());
        var wrongKind = new QuestionSet().Add("category", Question.Noul());

        Assert.True(Answers.TryGet(present, out NoulAnswer? answer));
        Assert.Equal(0.9, answer!.Noul);
        Assert.False(Answers.TryGet(missing, out NoulAnswer? none));
        Assert.Null(none);
        Assert.Throws<InvalidOperationException>(() => Answers.TryGet(wrongKind, out _));
    }

    [Fact]
    public void Dictionary_MembersBehaveLikeAReadOnlyDictionary()
    {
        Assert.Equal(5, Answers.Count);
        Assert.Equal(["billing", "tone", "category", "badTone", "urgency"], Answers.Keys);
        Assert.Equal(5, Answers.Values.Count());
        Assert.True(Answers.ContainsKey("tone"));
        Assert.False(Answers.ContainsKey("Tone"));
        Assert.True(Answers.TryGetValue("tone", out Answer? answer));
        Assert.Equal("choice", answer!.Type);
        Assert.False(Answers.TryGetValue("nope", out _));
        Assert.Equal(5, Answers.Count(pair => pair.Value is not null));
        Assert.Equal(5, ((System.Collections.IEnumerable)Answers).Cast<object>().Count());
    }

    [Fact]
    public void Indexer_MissingNameThrowsKeyNotFoundWithTheName()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => Answers["nope"]);

        Assert.Contains("'nope'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullNames_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Answers[null!]);
        Assert.Throws<ArgumentNullException>(() => Answers.ContainsKey(null!));
        Assert.Throws<ArgumentNullException>(() => Answers.TryGetValue(null!, out _));
    }
}

public class RetryPolicyTests
{
    [Fact]
    public void Default_HasTheDocumentedSettings()
    {
        RetryPolicy policy = RetryPolicy.Default;

        Assert.Equal(2, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.InitialBackoff);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.MaxBackoff);
        Assert.Equal(0.25, policy.BackoffJitter);
        Assert.True(policy.RespectRetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(60), policy.MaxRetryAfter);
        Assert.True(policy.RetryOnConnectionError);
        Assert.True(policy.RetryOnTimeout);
    }

    [Theory]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    [InlineData(529, true)]
    [InlineData(599, true)]
    [InlineData(200, false)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(409, false)]
    [InlineData(422, false)]
    [InlineData(600, false)]
    public void Default_RetriesTimeoutsRateLimitsAndServerErrors(int status, bool retried)
    {
        Assert.Equal(retried, RetryPolicy.Default.RetryableStatusCodes.Contains(status));
        Assert.Equal(retried, RetryPolicy.Default.ShouldRetry(status));
    }

    [Fact]
    public void None_DisablesRetries()
    {
        Assert.Equal(0, RetryPolicy.None.MaxRetries);
        Assert.Equal(RetryPolicy.Default with { MaxRetries = 0 }, RetryPolicy.None);
    }

    [Fact]
    public void With_DerivesAVariationWithoutChangingTheOriginal()
    {
        RetryPolicy derived = RetryPolicy.Default with { MaxRetries = 7, RetryOnTimeout = false };

        Assert.Equal(7, derived.MaxRetries);
        Assert.False(derived.RetryOnTimeout);
        Assert.Equal(2, RetryPolicy.Default.MaxRetries);
        Assert.True(RetryPolicy.Default.RetryOnTimeout);
    }

    [Fact]
    public void RetryableStatusCodes_CanBeExtendedWithoutAffectingTheDefault()
    {
        RetryPolicy extended = RetryPolicy.Default with { RetryableStatusCodes = RetryPolicy.Default.RetryableStatusCodes.Add(409) };

        Assert.True(extended.RetryableStatusCodes.Contains(409));
        Assert.False(RetryPolicy.Default.RetryableStatusCodes.Contains(409));
    }

    [Fact]
    public void ShouldRetry_FailureKinds_FollowTheirFlags()
    {
        RetryPolicy policy = RetryPolicy.Default with { RetryOnTimeout = false };

        Assert.False(policy.ShouldRetry(new ApiTimeoutException(TimeSpan.FromSeconds(1))));
        Assert.True(policy.ShouldRetry(new ApiConnectionException("down")));
        Assert.False((policy with { RetryOnConnectionError = false }).ShouldRetry(new ApiConnectionException("down")));
    }

    [Theory]
    [InlineData(-1, "`retry.maxRetries` must be a non-negative integer, got -1.")]
    [InlineData(int.MinValue, "`retry.maxRetries` must be a non-negative integer, got -2147483648.")]
    public void MaxRetries_Negative_Throws(int value, string message)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Default with { MaxRetries = value });

        Assert.StartsWith(message, exception.Message, StringComparison.Ordinal);
        Assert.Equal(nameof(RetryPolicy.MaxRetries), exception.ParamName);
    }

    [Fact]
    public void Backoff_NegativeValues_ThrowNamingTheField()
    {
        var initial = Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Default with { InitialBackoff = TimeSpan.FromMilliseconds(-1) });
        var max = Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Default with { MaxBackoff = TimeSpan.FromSeconds(-2) });
        var retryAfter = Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Default with { MaxRetryAfter = TimeSpan.FromSeconds(-3) });

        Assert.StartsWith("`retry.backoffInitialMs` must be a non-negative number of milliseconds, got -1.", initial.Message, StringComparison.Ordinal);
        Assert.StartsWith("`retry.backoffMaxMs` must be a non-negative number of milliseconds, got -2000.", max.Message, StringComparison.Ordinal);
        Assert.StartsWith("`retry.maxRetryAfterMs` must be a non-negative number of milliseconds, got -3000.", retryAfter.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Backoff_ZeroIsAllowed()
    {
        RetryPolicy policy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero, MaxBackoff = TimeSpan.Zero, MaxRetryAfter = TimeSpan.Zero };

        Assert.Equal(TimeSpan.Zero, policy.InitialBackoff);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BackoffJitter_OutsideZeroToOne_Throws(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.Default with { BackoffJitter = value });

        Assert.Contains("`retry.backoffJitter` must be between 0 and 1", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void BackoffJitter_Boundaries_AreAllowed(double value)
    {
        Assert.Equal(value, (RetryPolicy.Default with { BackoffJitter = value }).BackoffJitter);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(1000)]
    [InlineData(0)]
    [InlineData(-5)]
    public void RetryableStatusCodes_OutsideHttpRange_Throw(int code)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPolicy.Default with { RetryableStatusCodes = System.Collections.Immutable.ImmutableHashSet.Create(200, code) });

        Assert.StartsWith($"`retry.httpStatuses` must contain HTTP status codes, got {code}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryableStatusCodes_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RetryPolicy.Default with { RetryableStatusCodes = null! });
    }

    [Fact]
    public void RetryableStatusCodes_EmptySet_DisablesStatusRetries()
    {
        RetryPolicy policy = RetryPolicy.Default with { RetryableStatusCodes = System.Collections.Immutable.ImmutableHashSet<int>.Empty };

        Assert.False(policy.ShouldRetry(503));
    }

    [Fact]
    public void Equality_ComparesAllSettingsIncludingTheStatusSet()
    {
        RetryPolicy a = RetryPolicy.Default with { MaxRetries = 4 };
        RetryPolicy b = RetryPolicy.Default with { MaxRetries = 4 };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, RetryPolicy.Default);
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { RetryableStatusCodes = RetryPolicy.Default.RetryableStatusCodes.Remove(503) });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { RespectRetryAfter = false });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { MaxRetryAfter = TimeSpan.FromSeconds(1) });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { BackoffJitter = 0.5 });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { InitialBackoff = TimeSpan.FromSeconds(1) });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { MaxBackoff = TimeSpan.FromSeconds(1) });
        Assert.NotEqual(RetryPolicy.Default, RetryPolicy.Default with { RetryOnConnectionError = false });
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void ToString_ListsTheSettings()
    {
        string text = RetryPolicy.Default.ToString();

        Assert.Contains("MaxRetries = 2", text, StringComparison.Ordinal);
        Assert.Contains("InitialBackoff = 00:00:00.5000000", text, StringComparison.Ordinal);
        Assert.Contains("RetryableStatusCodes = [408, 429, 500,", text, StringComparison.Ordinal);
        Assert.Contains("BackoffJitter = 0.25", text, StringComparison.Ordinal);
    }
}
