using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Database;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Abstractions;

public sealed class InMemoryCartQueries(DatabaseConfig cfg, ILogger<InMemoryCartQueries> logger) : ICartQueries
{
    private readonly string _cs = cfg.ConnectionString;
    private readonly ILogger _logger = logger;

    public IO<Fin<CartSnapshot>> GetCart(Guid cartId) => IO.lift<Fin<CartSnapshot>>(() =>
    {
        try
        {
            using var conn = new SqliteConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, UserId FROM Carts WHERE Id=@Id LIMIT 1";
            cmd.Parameters.AddWithValue("@Id", cartId.ToString());
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return Fin<CartSnapshot>.Fail(Error.New($"Cart {cartId} not found"));
            var dbCartId = Guid.Parse(reader.GetString(0));
            var userId = Guid.Parse(reader.GetString(1));

            using var linesCmd = conn.CreateCommand();
            linesCmd.CommandText = "SELECT ProductId, Quantity, UnitPrice, Currency FROM CartLines WHERE CartId=@CartId";
            linesCmd.Parameters.AddWithValue("@CartId", dbCartId.ToString());
            using var lineReader = linesCmd.ExecuteReader();
            Map<ProductId, CartLine> map = Map<ProductId, CartLine>();
            while (lineReader.Read())
            {
                var productId = new ProductId(Guid.Parse(lineReader.GetString(0)));
                var qty = lineReader.GetInt32(1);
                var unitPrice = lineReader.GetDecimal(2);
                var currency = lineReader.GetString(3);
                map = map.Add(productId, new CartLine(productId, qty, new Money(currency, unitPrice)));
            }
            var subtotalAmt = map.Values.Fold(0m, (acc, line) => acc + (line.UnitPrice.Amount * line.Quantity));
            var currencyAll = map.Values.IsEmpty() ? "SEK" : map.Values.Head().Match(l => l.UnitPrice.Currency, () => "SEK");
            var snapshot = new CartSnapshot(dbCartId, userId, map, new Money(currencyAll, subtotalAmt), "SE", "NA");
            return Fin<CartSnapshot>.Succ(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCart failed for {CartId}", cartId);
            return Fin<CartSnapshot>.Fail(Error.New(ex));
        }
    });
}
