namespace SmallShopBigAmbitions.Models;

public record CustomerCart
(
     Guid Id,
     Guid UserId,
     Option<string[]> Items
);