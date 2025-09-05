using LanguageExt;
using SmallShopBigAmbitions.Application.Billing.Payments;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments;

public sealed class CreditService(ILogger<CreditService> logger) : ICreditService
{
    private readonly ILogger _logger = logger;

    public IO<Fin<Unit>> Apply(Guid orderId, string code)
    {
        return IO.lift<Fin<Unit>>(() =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Fin<Unit>.Fail(Error.New("Credit code required"));
            // Demo: pretend every code starting with SAVE is valid
            if (!code.StartsWith("SAVE", StringComparison.OrdinalIgnoreCase))
                return Fin<Unit>.Fail(Error.New("Invalid credit code"));
            _logger.LogInformation("Applied credit code {Code} to order {OrderId}", code, orderId);
            return FinSucc(unit);
        });
    }
}
