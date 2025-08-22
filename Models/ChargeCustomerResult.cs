namespace SmallShopBigAmbitions.Models;

public record ChargeResult(
 Option<string> Message,
 Guid Cart,
 Guid User,
 Guid Transaction,
 Guid Receipt
 );