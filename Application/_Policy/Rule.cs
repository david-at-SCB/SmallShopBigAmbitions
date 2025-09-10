using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Policy;

/// <summary>
/// Composable rule that returns Validation accumulating errors.
/// </summary>
public readonly record struct Rule(string Name, Func<Validation<Seq<Error>, Unit>> Run)
{
    public Validation<Seq<Error>, Unit> Validate() => Run();

    public static Rule From(string name, Func<bool> predicate, string errorCode) =>
        new(name, () => predicate() ? Success<Seq<Error>, Unit>(unit) : Fail<Seq<Error>, Unit>(Seq(Error.New(errorCode))));

    public static Rule Pass(string name) => new(name, () => Success<Seq<Error>, Unit>(unit));
}

public static class RuleCombiner
{
    /// <summary>
    /// Combine rules applicatively to accumulate all failures.
    /// </summary>
    public static Validation<Seq<Error>, Unit> Apply(params object[] parts)
    {
        var errors = Seq<Error>();
        foreach (var p in parts)
        {
            switch (p)
            {
                case Rule r:
                    {
                        var res = r.Validate();
                        res.Match(
                            Succ: _ => { },
                            Fail: es => errors += es
                        );
                        break;
                    }
                default:
                    if (p is Validation<Seq<Error>, Unit> v)
                    {
                        v.Match(
                            Succ: _ => { },
                            Fail: es => errors += es
                        );
                    }
                    break;
            }
        }
        return errors.IsEmpty ? Success<Seq<Error>, Unit>(unit) : Fail<Seq<Error>, Unit>(errors);
    }
}
