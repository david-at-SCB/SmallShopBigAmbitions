using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;

/// <summary>
/// Lightweight authorization policy for IntentToPayCommand.
/// Requires TrustedContext.IsAuthenticated or a non-empty caller id.
/// (Adjust for real requirements e.g., role checks / scope.)
/// </summary>
public sealed class IntentToPayAuthPolicy : IAuthorizationPolicy<IntentToPayCommand>
{
    public Fin<Unit> Authorize(IntentToPayCommand request, TrustedContext context)
        => (context.IsAuthenticated || context.CallerId != Guid.Empty)
            ? FinSucc(Unit.Default)
            : FinFail<Unit>(Error.New("payment.intent.unauthorized"));
}
