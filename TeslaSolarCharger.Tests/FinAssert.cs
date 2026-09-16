using LanguageExt;
using LanguageExt.Common;
using System;

namespace TeslaSolarCharger.Tests;

public static class FinAssert
{
    public static T Succ<T>(Fin<T> result) =>
        result.Match(Succ: value => value, Fail: error => throw Xunit.Sdk.FailException.ForFailure($"Expected success but got error: {error.Message}"));

    public static Error Fail<T>(Fin<T> result) =>
        result.Match(Succ: _ => throw Xunit.Sdk.FailException.ForFailure("Expected an error but got success"), Fail: error => error);

    public static Exception Exceptional<T>(Fin<T> result)
    {
        var error = Fail(result);
        if (!error.IsExceptional)
        {
            throw Xunit.Sdk.FailException.ForFailure($"Expected an exceptional error but got an expected one: {error.Message}");
        }
        return error.ToException();
    }
}
