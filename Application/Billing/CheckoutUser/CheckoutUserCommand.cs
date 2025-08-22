using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public record CheckoutUserCommand(Guid UserId)
    : IFunctionalRequest<UserCheckoutResult>;