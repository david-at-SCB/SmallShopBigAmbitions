using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application._Abstractions;

public enum CatalogProvider
{ FakeStore /*, More later */ }

public readonly record struct ExternalProductRef(CatalogProvider Provider, string ExternalId);

public interface IProductCatalog
{
    /// Map (Provider, ExternalId) -> ProductId, creating the mapping (and Product) if missing.
    IO<Fin<ProductId>> EnsureMapped(ExternalProductRef ext);

    /// Get the current catalog price for a product in a given currency.
    IO<Fin<Money>> GetCurrentPrice(ProductId id, string currency);
}