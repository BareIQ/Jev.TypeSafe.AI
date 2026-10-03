using System;
using System.Threading.Tasks;
using Xunit;

namespace TypeSafe.AI.Tests.Support;

/// <summary>Assertions about methods that must fail before returning a task.</summary>
internal static class Sync
{
    /// <summary>
    /// Asserts that the delegate throws exactly <typeparamref name="TException"/> <em>synchronously</em>, i.e. validation
    /// failed before any task was returned, rather than faulting a returned task.
    /// </summary>
    public static TException Throws<TException>(Func<Task> action)
        where TException : Exception
    {
        Task? returned = null;
        Exception? thrown = null;
        try
        {
            returned = action();
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        if (thrown is null)
        {
            // Observe the task so that its eventual fault is not reported as unobserved.
            _ = returned?.ContinueWith(task => task.Exception, TaskScheduler.Default);
            throw Xunit.Sdk.EqualException.ForMismatchedValues(typeof(TException).Name, "a task returned without a synchronous exception");
        }

        return Assert.IsType<TException>(thrown);
    }
}
