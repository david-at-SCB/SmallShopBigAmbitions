namespace SmallShopBigAmbitions.Models;

public readonly record struct ProductId(Guid Value);

/// Canonical cart line in the business layer
public sealed record CartLine(
    ProductId ProductId,
    int Quantity,
    Money UnitPrice);

/// Canonical cart snapshot in the business layer
public sealed record CartSnapshot(
    Cart Cart,
    CustomerId CustomerId,
    Money Subtotal,  // denormalized snapshot (can be recomputed)
    string Country,
    string Region,
    bool Valid,
    string[] Errors)
{ public Guid SnapShotId = new ();

    public int GetItemsAmount()
    {
        return Cart.GetAmountOfItems();
    }
    public bool CartIsNotEmpty() => !Cart.Items.IsEmpty;

    public Seq<CartLine> Lines => Seq(Cart.Items.Values);
};
