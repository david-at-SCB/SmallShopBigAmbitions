using LanguageExt;
using LanguageExt.Common;

namespace SmallShopBigAmbitions.Monads;

public readonly struct OptionT<T>
{
    private readonly Result<Option<T>> _value;

    public OptionT(Result<Option<T>> value) => _value = value;

    public OptionT<U> Bind<U>(Func<T, OptionT<U>> f)
    {
        return new OptionT<U>(_value.FlatMap(opt =>
            opt.Match( 
                Some: val => f(val)._value,
                None: () => new Result<Option<U>>(Option<U>.None)
            )));
    }

    public Result<Option<T>> Value => _value;
}

public static class ResultExtensions
{
    public static Result<U> FlatMap<T, U>(this Result<T> result, Func<T, Result<U>> binder)
    {
        return result.Match(
            Succ: binder,
            Fail: err => new Result<U>(err)
        );
    }
}