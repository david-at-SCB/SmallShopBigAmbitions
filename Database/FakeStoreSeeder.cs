using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;
using LanguageExt;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Database;

public class FakeStoreSeeder(ProductService _productService)
{
    /// <summary>
    /// Declarative, traceable seed: fetch products then upsert them using the DSL.
    /// Returns a TraceableT<Unit> so callers can compose/log and run as Fin.
    /// </summary>
    public TraceableT<Unit> Seed(string connectionString, CancellationToken ct = default)
    {
        // Step 1: fetch all products (throw on Fail to keep the pipeline simple)
        var fetch = TraceableTLifts.FromIOFinThrowing(
            _productService.GetAllProducts(ct: ct).RunTraceable(ct),
            spanName: "FetchFakeStoreProducts"
        );

        // Step 2: insert each product if not exists (as a single traced IO op)
        TraceableT<Unit> insertAll(Seq<ProductService.ProductDto> dtos) =>
            TraceableTLifts.FromIO(
                IO.lift(() =>
                {
                    using var connection = new SqliteConnection(connectionString);
                    connection.Open();

                    foreach (var dto in dtos)
                    {
                        var p = Mapper.MapToBusinessProduct(dto);
                        var (rate, count) = p.Rating.Match(r => (r.Rate, r.Count), () => (0.0, 0));

                        DatabaseInitialize.InsertIfNotExists(
                            connection,
                            table: "FakeStoreProducts",
                            keyColumn: "Id",
                            keyValue: p.Id,
                            values: new Dictionary<string, object?>
                            {
                                ["Id"] = p.Id,
                                ["Title"] = p.Title,
                                ["Price"] = p.Price,
                                ["Description"] = p.Description,
                                ["Category"] = p.Category,
                                ["Image"] = p.Image,
                                ["RatingRate"] = rate,
                                ["RatingCount"] = count
                            }
                        );
                    }

                    return unit;
                }),
                spanName: "SeedProductsIntoSqlite"
            );

        // Compose: fetch -> insert
        var pipeline = fetch.Bind(insertAll)
                            .WithSpanName("SeedFakeStorePipeline");

        return pipeline;
    }
}