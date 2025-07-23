using LanguageExt;
using LanguageExt.Common;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace SmallShopBigAmbitions.Monads;

public readonly struct TraceableOptionT<T>
{
    private readonly Traceable<Result<Option<T>>> _traceable;

    public TraceableOptionT(Traceable<Result<Option<T>>> traceable) =>
        _traceable = traceable;

    public async Task<TraceableOptionT<U>> BindAsync<U>(Func<T, Task<TraceableOptionT<U>>> f)
    {
        var result = await _traceable.RunTraceableAsync();

        return await result.Match(
            Succ: async opt =>
                await opt.Match(
                    Some: async val => await f(val),
                    None: () => Task.FromResult(TraceableOptionT<U>.None("none"))
                ),
            Fail: err => Task.FromResult(TraceableOptionT<U>.Fail(err, "fail"))
        );
    }

    public async Task<TraceableOptionT<U>> MapAsync<U>(Func<T, U> f, string spanName = "map")
    {
        var result = await _traceable.RunTraceableAsync();
        var mapped = result.Map(opt => opt.Map(f));
        return new TraceableOptionT<U>(new Traceable<Result<Option<U>>>(() => Task.FromResult(mapped), spanName));
    }

    public Task<Result<Option<T>>> RunAsync() => _traceable.RunTraceableAsync();

    public static TraceableOptionT<T> Lift(Traceable<Result<Option<T>>> traceable) =>
        new(traceable);

    public static TraceableOptionT<T> Some(T value, string spanName) =>
        new(new Traceable<Result<Option<T>>>(() => Task.FromResult(new Result<Option<T>>(Prelude.Some(value))), spanName));

    public static TraceableOptionT<T> None(string spanName) =>
        new(new Traceable<Result<Option<T>>>(() => Task.FromResult(new Result<Option<T>>(Option<T>.None)), spanName));

    public static TraceableOptionT<T> Fail(Error error, string spanName) =>
        new(new Traceable<Result<Option<T>>>(() => Task.FromResult(new Result<Option<T>>(error)), spanName));

    public TraceableOptionT<T> WithLogging(ILogger logger) =>
        new(_traceable.WithLogging(logger));

}