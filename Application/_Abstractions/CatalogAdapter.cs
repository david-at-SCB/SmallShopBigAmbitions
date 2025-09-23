using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application._Abstractions;

public enum CatalogProvider
{ FakeStore /*, More later */ }

//public readonly record struct ExternalProductRef(CatalogProvider Provider, string ExternalId);

public interface IProductCatalog
{
    /// Map (Provider, ExternalId) -> ProductId, creating the mapping (and Product) if missing.
    IO<Fin<ProductId>> EnsureMapped(ExternalProductRef ext);

    /// Get the current catalog price for a product in a given currency.
    IO<Fin<Money>> GetCurrentPrice(ProductId id, string currency);
}

public readonly record struct ExternalProductRef(int ApiProductId, CatalogProvider Provider);

public readonly record struct Quantity
{
    public int Value { get; }
    private Quantity(int value) => Value = value;

    public static Fin<Quantity> Create(int value) =>
        value > 0
            ? FinSucc(new Quantity(value))
            : FinFail<Quantity>(Error.New("Quantity must be > 0"));

    public static implicit operator int(Quantity q) => q.Value;
}