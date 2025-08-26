using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public record CheckoutUserCommand(Guid UserId)
    : IFunctionalRequest<CheckoutUserResultDTO>;