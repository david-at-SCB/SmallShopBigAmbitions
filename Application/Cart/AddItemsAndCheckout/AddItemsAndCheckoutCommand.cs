using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public record AddItemsAndCheckoutCommand(Guid UserId, IEnumerable<string> Items)
    : IFunctionalRequest<CheckoutUserResultDTO>;
