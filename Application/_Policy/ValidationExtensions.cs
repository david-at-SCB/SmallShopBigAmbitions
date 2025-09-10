using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Policy;

public static class ValidationExtensions
{
    public static Fin<Unit> ToFin(this Validation<Seq<Error>, Unit> validation) =>
        validation.Match(
            Succ: _ => FinSucc(unit),
            Fail: errs => FinFail<Unit>(Error.New(string.Join("; ", errs.Map(e => e.Message)))));
}
