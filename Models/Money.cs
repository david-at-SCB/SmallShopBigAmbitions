namespace SmallShopBigAmbitions.Models;

public sealed record Money(string Currency, decimal Amount)
{
    public static Money GetFromFin(Fin<Money> money)
    {
        return money.Match(
            Succ: m => m,
            Fail: _ => new Money("SEK", 0) // Default value in case of failure
        );
    }
}

public static class MoneyOps
{
    public static Money Plus(this Money a, Money b) =>
        a.Currency == b.Currency ? new Money(a.Currency, a.Amount + b.Amount)
                                 : throw new InvalidOperationException("Currency mismatch");

    public static Money Minus(this Money a, Money b) =>
        a.Currency == b.Currency ? new Money(a.Currency, a.Amount - b.Amount)
                                 : throw new InvalidOperationException("Currency mismatch");
}