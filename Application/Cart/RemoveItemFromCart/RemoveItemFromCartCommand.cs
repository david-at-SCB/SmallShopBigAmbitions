using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public record RemoveItemFromCartCommand_Full_Params(ProductId Product, Models.Cart Cart, Guid CustomerId) : IFunctionalRequest<RemoveItemFromCartDTO>;

public record RemoveItemFromCartCommand_Thin_Params(Guid ProductId, Guid CustomerId, Guid CartId) : IFunctionalRequest<RemoveItemFromCartDTO>;