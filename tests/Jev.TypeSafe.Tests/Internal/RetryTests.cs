using System;
using TypeSafe.AI.Internal.Retry;
using TypeSafe.AI.Testing;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class RetryMathTests
{
    [Theory]
    [InlineData(0, 500)]
    [InlineData(1, 1000)]
    [InlineData(2, 2000)]
    [InlineData(3, 4000)]
    [InlineData(4, 5000)]
    [InlineData(10, 5000)]
    public void ComputeDelay_WithoutJitter_GrowsExponentiallyAndCapsAtMaxBackoff(int attempt, int expectedMs)
    {
        TimeSpan delay = RetryMath.ComputeDelay(attempt, retryAfter: null, RetryPolicy.Default, random: 0);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
    }

    [Theory]
    [InlineData(0.0, 500)]
    [InlineData(0.5, 438)]
    [InlineData(0.999999, 375)]
    public void ComputeDelay_Jitter_SubtractsUpToTwentyFivePercent(double random, int expectedMs)
    {
        TimeSpan delay = RetryMath.ComputeDelay(0, null, RetryPolicy.Default, random);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
    }

    [Fact]
    public void ComputeDelay_ServerDelayWithinCeiling_IsUsedExactlyWithoutJitter()
    {
        TimeSpan delay = RetryMath.ComputeDelay(0, TimeSpan.FromSeconds(2), RetryPolicy.Default, random: 0.9);

        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void ComputeDelay_ServerDelayAtCeiling_IsStillHonored()
    {
        TimeSpan delay = RetryMath.ComputeDelay(0, RetryPolicy.Default.MaxRetryAfter, RetryPolicy.Default, 0);

        Assert.Equal(TimeSpan.FromSeconds(60), delay);
    }

    [Fact]
    public void ComputeDelay_ServerDelayAboveCeiling_FallsBackToBackoff()
    {
        TimeSpan delay = RetryMath.ComputeDelay(0, TimeSpan.FromSeconds(61), RetryPolicy.Default, 0);

        Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
    }

    [Fact]
    public void ComputeDelay_RespectRetryAfterDisabled_IgnoresServerDelay()
    {
        RetryPolicy policy = RetryPolicy.Default with { RespectRetryAfter = false };

        Assert.Equal(TimeSpan.FromMilliseconds(500), RetryMath.ComputeDelay(0, TimeSpan.FromSeconds(1), policy, 0));
    }

    [Fact]
    public void ComputeDelay_CustomCeiling_IsUsed()
    {
        RetryPolicy policy = RetryPolicy.Default with { MaxRetryAfter = TimeSpan.FromSeconds(5) };

        Assert.Equal(TimeSpan.FromSeconds(4), RetryMath.ComputeDelay(0, TimeSpan.FromSeconds(4), policy, 0));
        Assert.Equal(TimeSpan.FromMilliseconds(500), RetryMath.ComputeDelay(0, TimeSpan.FromSeconds(6), policy, 0));
    }

    [Fact]
    public void ComputeDelay_CustomInitialCapAndJitter_AreUsed()
    {
        RetryPolicy policy = RetryPolicy.Default with
        {
            InitialBackoff = TimeSpan.FromMilliseconds(100),
            MaxBackoff = TimeSpan.FromMilliseconds(300),
            BackoffJitter = 0.5,
        };

        Assert.Equal(TimeSpan.FromMilliseconds(100), RetryMath.ComputeDelay(0, null, policy, 0));
        Assert.Equal(TimeSpan.FromMilliseconds(300), RetryMath.ComputeDelay(5, null, policy, 0));
        Assert.Equal(TimeSpan.FromMilliseconds(150), RetryMath.ComputeDelay(5, null, policy, 1.0));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(int.MaxValue)]
    public void ComputeDelay_VeryLargeAttemptWithZeroInitialBackoff_DoesNotOverflowToNaN(int attempt)
    {
        RetryPolicy policy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero };

        Assert.Equal(TimeSpan.Zero, RetryMath.ComputeDelay(attempt, null, policy, 0));
    }

    [Fact]
    public void ComputeDelay_VeryLargeAttempt_CapsAtMaxBackoff()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), RetryMath.ComputeDelay(int.MaxValue, null, RetryPolicy.Default, 0));
    }

    [Fact]
    public void ComputeDelay_HugeMaxBackoff_NeverExceedsTimerLimit()
    {
        RetryPolicy policy = RetryPolicy.Default with { MaxBackoff = TimeSpan.FromDays(365) };

        TimeSpan delay = RetryMath.ComputeDelay(40, null, policy, 0);

        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), delay);
    }
}

public class RetryAfterParserTests
{
    private static readonly DateTimeOffset s_now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("1500", null, 1500.0)]
    [InlineData("1500", "9", 1500.0)]
    [InlineData("0", "9", 0.0)]
    [InlineData("12.5", null, 12.5)]
    [InlineData(null, "2", 2000.0)]
    [InlineData(null, "1.5", 1500.0)]
    [InlineData(null, "0", 0.0)]
    [InlineData("-5", "3", 3000.0)]
    [InlineData("abc", "3", 3000.0)]
    [InlineData("abc", null, null)]
    [InlineData("NaN", null, null)]
    [InlineData("Infinity", null, null)]
    [InlineData(null, "-1", null)]
    [InlineData(null, "soon", null)]
    [InlineData(null, "1e30", null)]
    [InlineData("", null, 0.0)]
    [InlineData(null, "  ", 0.0)]
    public void Parse_Values_ReturnsExpectedDelay(string? retryAfterMs, string? retryAfter, double? expectedMs)
    {
        TimeSpan? delay = RetryAfterParser.Parse(retryAfterMs, retryAfter, s_now);

        Assert.Equal(expectedMs is null ? null : TimeSpan.FromMilliseconds(expectedMs.Value), delay);
    }

    [Fact]
    public void Parse_HttpDateInFuture_ReturnsTimeUntilThen()
    {
        TimeSpan? delay = RetryAfterParser.Parse(null, "Thu, 01 Jan 2026 00:00:30 GMT", s_now);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void Parse_HttpDateInPast_ReturnsZero()
    {
        TimeSpan? delay = RetryAfterParser.Parse(null, "Wed, 31 Dec 2025 23:00:00 GMT", s_now);

        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void Parse_Response_ReadsHeadersCaseInsensitively()
    {
        RawResponse response = TypeSafeModelFactory.RawResponse(
            429,
            headers: [new("RETRY-AFTER-MS", "250"), new("retry-after", "9")]);

        Assert.Equal(TimeSpan.FromMilliseconds(250), RetryAfterParser.Parse(response, s_now));
    }

    [Fact]
    public void Parse_ResponseWithoutHeaders_ReturnsNull()
    {
        Assert.Null(RetryAfterParser.Parse(TypeSafeModelFactory.RawResponse(429), s_now));
    }
}
