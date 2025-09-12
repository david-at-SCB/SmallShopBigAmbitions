using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

// Command now carries identifiers; the handler will load the cart
public record ChargeCustomerCommand(Guid UserId, Guid CartId, Models.Cart Cart)
    : IFunctionalRequest<ChargeResult>
{
    // Convenience constructor to build the command from a Cart instance
    public ChargeCustomerCommand(Models.Cart cart, Guid CartId, Guid CustomerId)
        : this(CustomerId, CartId, cart) { }

    public string? GetSpanName() => "Billing.ChargeCustomer";
    public IEnumerable<(string Key, object? Value)> GetTraceAttributes() =>
        new[] { ("user.id", (object?)UserId) };

}