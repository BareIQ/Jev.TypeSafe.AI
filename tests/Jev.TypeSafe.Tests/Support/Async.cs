using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace TypeSafe.AI.Tests.Support;

/// <summary>Helpers for driving asynchronous work against a <see cref="FakeTimeProvider"/>.</summary>
internal static class Async
{
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(20);

    /// <summary>Waits for a condition, polling briefly; fails the test instead of hanging.</summary>
    public static async Task UntilAsync(Func<bool> condition, string what = "condition")
    {
        DateTime end = DateTime.UtcNow + s_deadline;
        while (!condition())
        {
            if (DateTime.UtcNow > end)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(2);
        }
    }

    /// <summary>Advances fake time in steps until the task completes, then returns its result or rethrows.</summary>
    public static async Task<T> AdvanceUntilCompletedAsync<T>(this FakeTimeProvider time, Task<T> task, TimeSpan? step = null)
    {
        await time.AdvanceUntilAsync(() => task.IsCompleted, step);
        return await task;
    }

    /// <summary>Advances fake time in steps until the task completes.</summary>
    public static async Task AdvanceUntilCompletedAsync(this FakeTimeProvider time, Task task, TimeSpan? step = null)
    {
        await time.AdvanceUntilAsync(() => task.IsCompleted, step);
        await task;
    }

    /// <summary>Advances fake time in steps until a condition holds.</summary>
    public static async Task AdvanceUntilAsync(this FakeTimeProvider time, Func<bool> condition, TimeSpan? step = null)
    {
        TimeSpan amount = step ?? TimeSpan.FromSeconds(1);
        DateTime end = DateTime.UtcNow + s_deadline;
        while (!condition())
        {
            if (DateTime.UtcNow > end)
            {
                throw new TimeoutException("Timed out advancing fake time.");
            }

            time.Advance(amount);
            await Task.Delay(2);
        }
    }

    /// <summary>Awaits a task that is expected to fail, returning the exception.</summary>
    public static async Task<TException> FailsWithAsync<TException>(Task task)
        where TException : Exception
        => await Assert.ThrowsAsync<TException>(async () => await task);

    /// <summary>Awaits a task with a deadline so that a hang fails the test.</summary>
    public static async Task<T> WithDeadlineAsync<T>(this Task<T> task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(s_deadline));
        if (finished != task)
        {
            throw new TimeoutException("The operation did not complete in time.");
        }

        return await task;
    }

    /// <summary>Awaits a task with a deadline so that a hang fails the test.</summary>
    public static async Task WithDeadlineAsync(this Task task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(s_deadline));
        if (finished != task)
        {
            throw new TimeoutException("The operation did not complete in time.");
        }

        await task;
    }

    /// <summary>Returns the exception a delegate throws, or <see langword="null"/>.</summary>
    public static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    public static IEnumerable<T> One<T>(T value)
    {
        yield return value;
    }
}
