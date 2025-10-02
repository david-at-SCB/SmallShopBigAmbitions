using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutGatherInfoStep;

//Guid cartId, Customer customer, PaymentMethod method, string? IdempotencyKey, Money Amount, string shippingAddress, Map<string, string> metaData

public record CheckoutProcedureResult(Guid CartId, Customer Customer, PaymentMethod Method, string? IdempotencyKey, Money Amount, string ShippingAddress, Map<string, string> MetaData)
{
}
