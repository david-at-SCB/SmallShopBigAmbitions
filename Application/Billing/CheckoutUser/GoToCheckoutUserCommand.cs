using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public record GoToCheckoutUserCommand(Guid Customer, Models.Cart Cart, Option<string> Currency)
    : IFunctionalRequest<CheckoutUserResultDTO>;