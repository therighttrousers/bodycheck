using System;
using System.Runtime.ExceptionServices;

using GdUnit4.Core.Execution.Exceptions;

namespace BodyCheck.Tests;

public static class Exceptions
{
    /// <summary>
    /// The exception of type <typeparamref name="T"/> that <paramref name="action"/> throws, failing the test if it
    /// throws none. gdUnit4's <c>AssertThrown</c> checks the innermost exception instead of the one thrown, so it
    /// can't check one that wraps another, as the readers do to put a file's path in a parser's message.
    /// </summary>
    public static T Thrown<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T e)
        {
            return e;
        }
        throw new TestFailedException($"Expected {typeof(T).Name} to be thrown, but nothing was.");
    }

    /// <summary>
    /// A sink for the unhandled failures of a scheduler whose tasks aren't expected to fail: it throws the failure,
    /// with its stack trace, out of the scheduler's run, failing the test.
    /// </summary>
    public static void Rethrow(Exception e) => ExceptionDispatchInfo.Throw(e);
}
