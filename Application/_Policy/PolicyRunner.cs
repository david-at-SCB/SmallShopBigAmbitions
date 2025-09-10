using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Policy;

public static class PolicyRunner
{
    /// <summary>
    /// Run a validation-producing function inside a traceable span with automatic error code tagging.
    /// </summary>
    public static TraceableT<Fin<Unit>> Run(
        string policyName,
        Func<Validation<Seq<Error>, Unit>> validate,
        Action<Fin<Unit>>? after = null)
    {
        // Force the type to IO<Fin<Unit>> to avoid inference to IO<Unit>
        IO<Fin<Unit>> fin = IO<Fin<Unit>>.Lift(() =>
        {
            var v = validate();
            var result = v.Match(
                Succ: _ => FinSucc(Unit.Default),
                Fail: errs =>
                {
                    var joined = string.Join("; ", errs.Map(er => er.Message));
                    return FinFail<Unit>(Error.New(joined));
                });
            after?.Invoke(result);
            return result;
        });

        return TraceableTLifts
            .FromIOFin(fin, policyName)
            .WithErrorCodeTags();
    }

    /// <summary>
    /// Convenience: validate using already supplied Validation.
    /// </summary>
    public static TraceableT<Fin<Unit>> FromValidation(string policyName, Validation<Seq<Error>, Unit> validation) =>
        Run(policyName, () => validation);
}