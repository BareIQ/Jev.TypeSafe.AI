// Run with `dotnet run --project examples/Demo`. Needs TYPESAFE_API_KEY in the environment.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using TypeSafe.AI;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var client = new TypeSafeClient(new TypeSafeClientOptions { LogLevel = TypeSafeLogLevel.Info });

try
{
    IReadOnlyList<ModelCard> models = await client.Models.ListAsync(cancellationToken: cts.Token);
    Console.WriteLine($"Available models: {string.Join(", ", models.Select(model => model.Name))}");

    var ticket = new JsonObject
    {
        ["subject"] = "Charged twice this month",
        ["body"] = "Hi, I see two charges of $49 on my card for August. I only have one account. Please fix this ASAP, I'm pretty frustrated.",
    };

    // Adding a question returns a typed key; reading the answer with it needs no casts.
    var questions = new QuestionSet();
    var isBilling = questions.Add("isBilling", Question.Noul("Is this ticket about billing?"));
    var sentiment = questions.Add("sentiment", Question.Choice<Tone>("What is the customer's tone?"));
    var urgency = questions.Add("urgency", Question.Score("How urgent is this ticket?", "can wait", "this week", "today", "right now"));
    var refundRisk = questions.Add("refundRisk", Question.Score("How likely is the customer to demand a refund?", "unlikely", "possible", "likely"));

    ApiResponse<SystemOneResult> response = await client.SystemOneWithResponseAsync(new SystemOneRequest(ticket, questions), cancellationToken: cts.Token);
    SystemOneResult result = response.Value;

    ChoiceAnswer<Tone> tone = result.Answers.Get(sentiment);
    ScoreAnswer urgent = result.Answers.Get(urgency);
    ScoreAnswer refund = result.Answers.Get(refundRisk);

    Console.WriteLine($"billing?     {result.Answers.Get(isBilling).Noul.ToString("0.00", CultureInfo.InvariantCulture)}");
    Console.WriteLine($"tone         {tone.Value} ({tone.ProbabilitiesByValue[tone.Value].ToString("0.00", CultureInfo.InvariantCulture)})");
    Console.WriteLine($"urgency      {urgent.Score.ToString("0.00", CultureInfo.InvariantCulture)} on a 0-3 scale: {string.Join(", ", urgent.Legend.Select(level => $"{level.Key}={level.Value.AsText()}"))}");
    Console.WriteLine($"refund risk  {refund.Score.ToString("0.00", CultureInfo.InvariantCulture)} ({refund.Confidence.ToString("0.00", CultureInfo.InvariantCulture)} confidence)");
    Console.WriteLine($"tokens       {result.Usage.InputTokens} in / {result.Usage.OutputTokens} out (request {response.RequestId ?? "unknown"})");
    return 0;
}
catch (ApiException exception)
{
    Console.Error.WriteLine($"API error {exception.StatusCode} (request {exception.RequestId ?? "unknown"}): {exception.BodyText}");
    return 1;
}
catch (ApiUserAbortException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}

// Labels sent to the API are the member names, or the EnumMember value when present.
internal enum Tone
{
    [EnumMember(Value = "calm")]
    Calm,
    [EnumMember(Value = "frustrated")]
    Frustrated,
    [EnumMember(Value = "angry")]
    Angry,
}
