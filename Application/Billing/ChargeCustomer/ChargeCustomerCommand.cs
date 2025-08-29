using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

// Command now carries identifiers; the handler will load the cart
public record ChargeCustomerCommand(Guid UserId, Guid CartId)
    : IFunctionalRequest<ChargeResult>
{
    // Convenience constructor to build the command from a Cart instance
    public ChargeCustomerCommand(Models.Cart cart)
        : this(cart.CustomerId, cart.Id) { }
}