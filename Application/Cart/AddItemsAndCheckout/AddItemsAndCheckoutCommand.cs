using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public record AddItemsAndCheckoutCommand(Guid UserId, IEnumerable<string> Items)
    : IFunctionalRequest<UserCheckoutResult>;
