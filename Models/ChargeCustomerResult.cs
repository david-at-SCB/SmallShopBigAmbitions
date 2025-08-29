namespace SmallShopBigAmbitions.Models;

public record ChargeResult(
 Option<string> Message,
 Guid Cart,
 Guid Customer,
 Guid Transaction,
 Guid Receipt
 );