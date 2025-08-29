using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public record AddItemsToCartCommand(Guid CustomerId, Map<FakeStoreProduct, int> Items)
    : IFunctionalRequest<CheckoutUserResultDTO>;
