namespace SmallShopBigAmbitions.Monads;

using LanguageExt;
using static LanguageExt.Prelude;

public static class IOFin
{
    // Lift a value into IO<Fin<T>> (fail if value is null)
    public static IO<Fin<T>> Return<T>(T value) =>
        IO.lift<Fin<T>>(() =>
            value is null
                ? FinFail<T>(Error.New($"Cannot lift null value of type {typeof(T).Name} into Fin"))
                : FinSucc(value)
        );

    // Lift a function into IO<Fin<T>> (catch exceptions -> Fin.Fail)
    public static IO<Fin<T>> From<T>(Func<T> func) =>
        IO.lift<Fin<T>>(() =>
        {
            try
            {
                return FinSucc(func());
            }
            catch (Exception ex)
            {
                return FinFail<T>(Error.New(ex));
            }
        });

    // Lift a Fin<T> into IO<Fin<T>>
    public static IO<Fin<T>> FromFin<T>(Fin<T> fin) => IO.lift<Fin<T>>(() => fin);

    // Lift an Option<T> into IO<Fin<Option<T>>> (as success, preserving None/Some)
    public static IO<Fin<Option<T>>> FromOption<T>(Option<T> opt) => IO.lift<Fin<Option<T>>>(() => FinSucc(opt));

    // Convenience: Lift Option.None into IO<Fin<Option<T>>> (success with None)
    public static IO<Fin<Option<T>>> FromNone<T>() => IO.lift<Fin<Option<T>>>(() => FinSucc(Option<T>.None));

    // Convenience: Lift Some(value) into IO<Fin<Option<T>>> (success with Some)
    public static IO<Fin<Option<T>>> FromSome<T>(T value) => IO.lift<Fin<Option<T>>>(() => FinSucc(Option<T>.Some(value)));

    // Convenience: Create a failed IO<Fin<T>> with provided error
    public static IO<Fin<T>> Fail<T>(Error error) => IO.lift<Fin<T>>(() => FinFail<T>(error));

    // Convenience: Create a failed IO<Fin<Option<T>>> with provided error
    public static IO<Fin<Option<T>>> FailOption<T>(Error error) => IO.lift<Fin<Option<T>>>(() => FinFail<Option<T>>(error));

    // Lift a Unit into IO<Fin<Unit>> (success)
    public static IO<Fin<Unit>> ReturnUnit() => IO.lift<Fin<Unit>>(() => FinSucc(unit));
}
