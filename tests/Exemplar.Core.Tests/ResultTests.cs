using Exemplar.Core.Errors;
using Exemplar.Core.Functional;
using FluentAssertions;
using Xunit;

namespace Exemplar.Core.Tests;

public class ResultTests
{
    private sealed record TestError(string Message) : BaseError(Message);

    // ──────────────────────────────────────────────────────────────
    // Construction
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Success_WhenCreated_IsSuccessIsTrue()
    {
        var result = Result<TestError, int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_WhenCreated_IsFailureIsTrue()
    {
        var error = new TestError("something went wrong");
        var result = Result<TestError, int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    // ──────────────────────────────────────────────────────────────
    // Implicit conversions
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<TestError, int> result = 99;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(99);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        var error = new TestError("implicit failure");
        Result<TestError, int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    // ──────────────────────────────────────────────────────────────
    // Map
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<TestError, int>.Success(5);

        var mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_PropagatesErrorWithoutInvokingMapper()
    {
        var error = new TestError("map failure");
        var result = Result<TestError, int>.Failure(error);
        var mapperCalled = false;

        var mapped = result.Map(x =>
        {
            mapperCalled = true;
            return x * 2;
        });

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
        mapperCalled.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // Bind
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_OnSuccess_InvokesBinderAndReturnsResult()
    {
        var result = Result<TestError, int>.Success(3);

        var bound = result.Bind(x => Result<TestError, string>.Success($"value={x}"));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("value=3");
    }

    [Fact]
    public void Bind_OnFailure_ShortCircuitsWithoutInvokingBinder()
    {
        var error = new TestError("bind failure");
        var result = Result<TestError, int>.Failure(error);
        var binderCalled = false;

        var bound = result.Bind(x =>
        {
            binderCalled = true;
            return Result<TestError, string>.Success($"value={x}");
        });

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
        binderCalled.Should().BeFalse();
    }

    [Fact]
    public void Bind_WhenBinderReturnsFailure_PropagatesFailure()
    {
        var downstreamError = new TestError("downstream failure");
        var result = Result<TestError, int>.Success(1);

        var bound = result.Bind(_ => Result<TestError, string>.Failure(downstreamError));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(downstreamError);
    }

    // ──────────────────────────────────────────────────────────────
    // BindAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BindAsync_OnSuccess_InvokesAsyncBinderAndReturnsResult()
    {
        var result = Result<TestError, int>.Success(7);

        var bound = await result.BindAsync(async x =>
        {
            await Task.Yield();
            return Result<TestError, string>.Success($"async={x}");
        });

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("async=7");
    }

    [Fact]
    public async Task BindAsync_OnFailure_ShortCircuitsWithoutInvokingBinder()
    {
        var error = new TestError("async bind failure");
        var result = Result<TestError, int>.Failure(error);
        var binderCalled = false;

        var bound = await result.BindAsync(async x =>
        {
            binderCalled = true;
            await Task.Yield();
            return Result<TestError, string>.Success($"async={x}");
        });

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
        binderCalled.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // Match
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Match_OnSuccess_InvokesSuccessHandler()
    {
        var result = Result<TestError, int>.Success(100);

        var output = result.Match(
            onSuccess: v => $"success:{v}",
            onFailure: e => $"failure:{e.Message}");

        output.Should().Be("success:100");
    }

    [Fact]
    public void Match_OnFailure_InvokesFailureHandler()
    {
        var error = new TestError("match error");
        var result = Result<TestError, int>.Failure(error);

        var output = result.Match(
            onSuccess: v => $"success:{v}",
            onFailure: e => $"failure:{e.Message}");

        output.Should().Be("failure:match error");
    }

    // ──────────────────────────────────────────────────────────────
    // Chain (verify multi-step pipelines short-circuit early)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Chain_WhenFirstStepFails_SubsequentStepsAreNotInvoked()
    {
        var error = new TestError("step 1 failed");
        var step2Called = false;
        var step3Called = false;

        var result = Result<TestError, int>.Failure(error)
            .Bind(x =>
            {
                step2Called = true;
                return Result<TestError, int>.Success(x + 1);
            })
            .Bind(x =>
            {
                step3Called = true;
                return Result<TestError, string>.Success(x.ToString());
            });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        step2Called.Should().BeFalse();
        step3Called.Should().BeFalse();
    }
}
