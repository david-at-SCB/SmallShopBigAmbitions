using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using LanguageExt;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Database;

public static class DatabaseInitialize
{
    // Simple declarative schema model
    private sealed record Column(string Name, string Type, bool Nullable = true, bool PrimaryKey = false)
    {
        public string ToSql()
            => $"{Name} {Type}{(Nullable ? string.Empty : " NOT NULL")}{(PrimaryKey ? " PRIMARY KEY" : string.Empty)}";
    }

    private sealed record Table(string Name, IReadOnlyList<Column> Columns)
    {
        public string ToCreateSql()
        {
            var cols = string.Join(",\n                ", Columns.Select(c => c.ToSql()));
            return $@"CREATE TABLE IF NOT EXISTS {Name} (
                {cols}
            );";
        }
    }

    private static readonly IReadOnlyList<Table> Schema =
    [
        new Table(
            Name: "Customers",
            Columns:
            [
                new("Id", "TEXT", Nullable: false, PrimaryKey: true),
                new("Name", "TEXT"),
                new("Email", "TEXT"),
                new("DiscountTier", "INTEGER")
            ]
        ),
        new Table(
            Name: "FakeStoreProducts",
            Columns:
            [
                new("Id", "INTEGER", Nullable: false, PrimaryKey: true),
                new("Title", "TEXT"),
                new("Price", "REAL"),
                new("Description", "TEXT"),
                new("Category", "TEXT"),
                new("Image", "TEXT"),
                new("RatingRate", "REAL"),
                new("RatingCount", "INTEGER")
            ]
        ),
        new Table(
            Name: "Carts",
            Columns:
            [
                new("Id", "TEXT", Nullable: false, PrimaryKey: true),
                new("UserId", "TEXT")
            ]
        ),
        new Table(
            Name: "CustomerCarts",
            Columns:
            [
                new("Id", "TEXT", Nullable: false, PrimaryKey: true),
                new("UserId", "TEXT"),
                new("Items", "TEXT") // Serialized array of product IDs
            ]
        ),
        // New: Payment intents storage
        new Table(
            Name: "PaymentIntents",
            Columns:
            [
                new("Id", "TEXT", Nullable: false, PrimaryKey: true),
                new("CartId", "TEXT", Nullable: false),
                new("UserId", "TEXT", Nullable: false),
                new("Provider", "TEXT", Nullable: false),
                new("ProviderIntentId", "TEXT", Nullable: false),
                new("Currency", "TEXT", Nullable: false),
                new("Amount", "REAL", Nullable: false),
                new("Status", "INTEGER", Nullable: false),
                new("ClientSecret", "TEXT"),
                new("IdempotencyKey", "TEXT"),
                new("Metadata", "TEXT"),
                new("CreatedAt", "TEXT", Nullable: false),
                new("UpdatedAt", "TEXT", Nullable: false),
                new("ExpiresAt", "TEXT"),
                new("ReservationId", "TEXT", Nullable: false)
            ]
        ),
        // New: Idempotency key mapping
        new Table(
            Name: "IdempotencyKeys",
            Columns:
            [
                new("Key", "TEXT", Nullable: false, PrimaryKey: true),
                new("PaymentIntentId", "TEXT", Nullable: false)
            ]
        )
    ];

    public static IO<Fin<Unit>> Initialize(string connectionString) => IO.lift<Fin<Unit>>(() =>
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create schema declaratively
        foreach (var createSql in Schema.Select(t => t.ToCreateSql()))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
        }

        // Seed data declaratively
        InsertIfNotExists(
            connection,
            table: "Customers",
            keyColumn: "Id",
            keyValue: "11111111-1111-1111-1111-111111111111",
            values: new Dictionary<string, object>
            {
                ["Id"] = "11111111-1111-1111-1111-111111111111",
                ["Name"] = "Alice",
                ["Email"] = "alice@example.com",
                ["DiscountTier"] = 3
            }
        );

        InsertIfNotExists(
            connection,
            table: "FakeStoreProducts",
            keyColumn: "Id",
            keyValue: 1,
            values: new Dictionary<string, object>
            {
                ["Id"] = 1,
                ["Title"] = "Sample Product",
                ["Price"] = 19.99,
                ["Description"] = "A great product",
                ["Category"] = "General",
                ["Image"] = "image.png",
                ["RatingRate"] = 4.5,
                ["RatingCount"] = 100
            }
        );

        return Fin<Unit>.Succ(unit);
    });

    // Expose the insert helper so other seeders can reuse the DSL
    public static void InsertIfNotExists(SqliteConnection connection, string table, string keyColumn, object keyValue, IReadOnlyDictionary<string, object?> values)
    {
        var columns = string.Join(", ", values.Keys);
        var parameters = string.Join(", ", values.Keys.Select(k => "@" + k));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {table} ({columns})
            SELECT {parameters}
            WHERE NOT EXISTS (SELECT 1 FROM {table} WHERE {keyColumn} = @__key);
        ";

        foreach (var kv in values)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@" + kv.Key;
            p.Value = kv.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        var keyParam = cmd.CreateParameter();
        keyParam.ParameterName = "@__key";
        keyParam.Value = keyValue;
        cmd.Parameters.Add(keyParam);

        cmd.ExecuteNonQuery();
    }
}