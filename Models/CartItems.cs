using LanguageExt;

namespace SmallShopBigAmbitions.Models;

/// <summary>
/// Value object wrapper around the cart's collection of lines.
/// Centralizes invariants and operations on the line set.
/// </summary>
public sealed record CartItems(HashMap<ProductId, CartLine> Lines)
{
    public static CartItems Empty => new([]);

    public bool IsEmpty => Lines.IsEmpty;
    public int Count => Lines.Count;

    public Option<CartLine> Find(ProductId id) => Lines.Find(id);

    public CartItems Add(ProductId id, int qty, Money price) =>
        qty <= 0
            ? this
            : Find(id).Match(
                Some: line => this with { Lines = Lines.SetItem(id, line with { Quantity = line.Quantity + qty }) },
                None: () => this with { Lines = Lines.Add(id, new CartLine(id, qty, price)) });

    public CartItems SetQuantity(ProductId id, int qty) =>
        qty <= 0
            ? this with { Lines = Lines.Remove(id) }
            : Find(id).Match(
                Some: line => this with { Lines = Lines.SetItem(id, line with { Quantity = qty }) },
                None: () => this); // no-op if absent

    public IEnumerable<CartLine> Values => Lines.Values;
}
