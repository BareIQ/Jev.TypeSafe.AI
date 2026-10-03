using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Live;

/// <summary>
/// Tests against the real TypeSafe AI API. They are skipped unless <c>TYPESAFE_API_KEY</c> is set and cost a few
/// small requests when they run. Nothing here prints the key.
/// </summary>
public class LiveApiTests
{
    private const string SkipReason = "Set TYPESAFE_API_KEY to run the live API tests.";

    public static bool HasApiKey => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TypeSafeEnvironmentVariables.ApiKey));

    private static readonly JsonObject Ticket = new()
    {
        ["subject"] = "Charged twice this month",
        ["body"] = "I see two charges of $49 on my card for August. I only have one account. Please fix this ASAP.",
    };

    private static TypeSafeClient Client() => new(new TypeSafeClientOptions { Timeout = TimeSpan.FromSeconds(120), LogLevel = TypeSafeLogLevel.Off });

    private static double Sum(IEnumerable<double> values) => values.Sum();

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public async Task ListsModels()
    {
        using TypeSafeClient client = Client();

        ApiResponse<IReadOnlyList<ModelCard>> response = await client.Models.ListWithResponseAsync();

        Assert.StartsWith("req_", response.RequestId, StringComparison.Ordinal);
        Assert.NotEmpty(response.Value);
        Assert.All(response.Value, model =>
        {
            Assert.False(string.IsNullOrEmpty(model.Name));
            Assert.NotNull(model.Description);
            Assert.NotNull(model.ReleaseDate);
        });
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public async Task AnswersNoulChoiceAndScoreQuestionsWithTypedAnswers()
    {
        using TypeSafeClient client = Client();
        var questions = new QuestionSet();
        var isBilling = questions.Add("isBilling", Question.Noul("Is this ticket about billing?"));
        var tone = questions.Add("sentiment", Question.Choice<Tone>("What is the customer's tone?"));
        var category = questions.Add("category", Question.Choice("What is this ticket about?", ChoiceCriteria.FromLabels("billing", "technical", "other")));
        var urgency = questions.Add("urgency", Question.Score("How urgent is this ticket?", "can wait", "this week", "today"));

        ApiResponse<SystemOneResult> response = await client.SystemOneWithResponseAsync(new SystemOneRequest(Ticket, questions));

        SystemOneResult result = response.Value;
        Assert.StartsWith("req_", response.RequestId, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(result.Model));
        Assert.True(result.Usage.InputTokens > 0);
        Assert.True(result.Usage.OutputTokens >= 0);

        NoulAnswer billing = result.Answers.Get(isBilling);
        Assert.InRange(billing.Noul, 0, 1);

        ChoiceAnswer<Tone> sentiment = result.Answers.Get(tone);
        Assert.True(Enum.IsDefined(typeof(Tone), sentiment.Value));
        Assert.Equal(["Angry", "Calm", "frustrated"], sentiment.Probabilities.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.InRange(Sum(sentiment.Probabilities.Values), 0.9, 1.1);
        Assert.True(sentiment.Confidence >= 0);

        ChoiceAnswer picked = result.Answers.Get(category);
        Assert.Contains(picked.Choice, new[] { "billing", "technical", "other" });

        ScoreAnswer score = result.Answers.Get(urgency);
        Assert.InRange(score.Score, 0, 2);
        Assert.Equal(["can wait", "this week", "today"], score.Legend.OrderBy(pair => pair.Key).Select(pair => pair.Value.AsText()));
        Assert.Equal([0, 1, 2], score.Probabilities.Keys.OrderBy(k => k));
        Assert.InRange(Sum(score.Probabilities.Values), 0.9, 1.1);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public async Task AcceptsRichDescriptionsAndOneSidedNoulCriteria()
    {
        using TypeSafeClient client = Client();
        var questions = new QuestionSet();
        var duplicate = questions.Add(
            "duplicate",
            Question.Noul(
                "Is the customer reporting a duplicate charge?",
                new NoulCriteria { True = new JsonObject { ["meaning"] = "the same amount charged more than once", ["examples"] = new JsonArray("billed twice") } }));
        var tone = questions.Add(
            "tone",
            Question.Choice(
                "Tone?",
                new ChoiceCriteria
                {
                    { "calm", new JsonObject { ["summary"] = "measured", ["examples"] = new JsonArray("please look into this") } },
                    { "upset", Entry.Null },
                }));

        SystemOneResult result = await client.SystemOneAsync(new SystemOneRequest(Ticket, questions));

        Assert.InRange(result.Answers.Get(duplicate).Noul, 0, 1);
        Assert.Equal(["calm", "upset"], result.Answers.Get(tone).Probabilities.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public async Task RejectsABadApiKeyWithAuthenticationException()
    {
        using var client = new TypeSafeClient(new TypeSafeClientOptions
        {
            ApiKey = "not-a-real-key",
            RetryPolicy = RetryPolicy.None,
            LogLevel = TypeSafeLogLevel.Off,
        });

        await Assert.ThrowsAsync<AuthenticationException>(() => client.Models.ListAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public async Task RejectsAnUnknownModelWithAReadableBadRequestException()
    {
        using TypeSafeClient client = Client();
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("Is this a greeting?"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => client.SystemOneAsync(new SystemOneRequest("hello", questions) { Model = "no-such-model" }, new RequestOptions { RetryPolicy = RetryPolicy.None }));

        Assert.Equal("400 Unknown model: no-such-model", exception.Message);
        Assert.StartsWith("req_", exception.RequestId, StringComparison.Ordinal);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(HasApiKey), SkipType = typeof(LiveApiTests))]
    public void CatchesShapesTheApiWouldRejectBeforeSending()
    {
        using TypeSafeClient client = Client();
        var oneScore = new QuestionSet();
        oneScore.Add("q", Question.Score("?", "only one"));

        Sync.Throws<TypeSafeException>(() => client.SystemOneAsync(new SystemOneRequest("x", new QuestionSet())));
        Sync.Throws<TypeSafeException>(() => client.SystemOneAsync(new SystemOneRequest("x", oneScore)));
    }
}
