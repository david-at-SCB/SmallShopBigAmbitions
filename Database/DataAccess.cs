using LanguageExt;
using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application.Billing.Payments.CreatePaymentIntent;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;
using System;
using System.Linq;
using System.Collections.Generic;

namespace SmallShopBigAmbitions.Database;

// Environment for the Reader: keep DB config here
public sealed record DbEnv(string ConnectionString);

public interface IDataAccess
{
    TraceableT<Fin<Option<PaymentIntent>>> GetPaymentIntentById(Guid id);
    TraceableT<Fin<Unit>> UpdatePaymentIntent(PaymentIntent intent);
    TraceableT<Fin<PaymentIntent>> InsertPaymentIntent(PaymentIntent intent);
    TraceableT<Fin<Customer>> GetCustomerById(Guid userId);
    TraceableT<Fin<Cart>> GetCustomerCart(Guid userId);
}

public sealed class DataAccess(string connectionString) : IDataAccess
{
    private readonly DbEnv _env = new(connectionString);

    // Central helper: runs a synchronous Fin-producing function inside a TraceableT span with auto error tagging
    private static TraceableT<Fin<T>> TraceFin<T>(
        string span,
        Func<Fin<T>> run,
        Func<Fin<T>, IEnumerable<KeyValuePair<string, object>>>? attrs = null)
    {
        IO<Fin<T>> effect = IO.lift<Fin<T>>(() => run());
        IEnumerable<KeyValuePair<string, object>> AttrsFn(Fin<T> fin)
        {
            if (attrs != null) return attrs(fin);
            return fin.Match(
                Succ: _ => Enumerable.Empty<KeyValuePair<string, object>>(),
                Fail: e => new[] { new KeyValuePair<string, object>("error", e.Message) }
            );
        }
        return new TraceableT<Fin<T>>(effect, span, AttrsFn);
    }

    public TraceableT<Fin<Option<PaymentIntent>>> GetPaymentIntentById(Guid id) =>
        TraceFin("db.payment_intents.get_by_id", () =>
        {
            try
            {
                using var conn = new SqliteConnection(_env.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM PaymentIntents WHERE Id=@Id LIMIT 1";
                cmd.Parameters.AddWithValue("@Id", id.ToString());
                using var reader = cmd.ExecuteReader();
                return FinSucc(reader.Read()
                    ? Option<PaymentIntent>.Some(ReadIntent(reader))
                    : Option<PaymentIntent>.None);
            }
            catch (Exception ex)
            {
                return Fin<Option<PaymentIntent>>.Fail(Error.New(ex));
            }
        });

    public TraceableT<Fin<Unit>> UpdatePaymentIntent(PaymentIntent intent) =>
        TraceFin("db.payment_intents.update", () =>
        {
            try
            {
                using var conn = new SqliteConnection(_env.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE PaymentIntents SET
                    Provider=@Provider,
                    ProviderIntentId=@ProviderIntentId,
                    Currency=@Currency,
                    Amount=@Amount,
                    Status=@Status,
                    ClientSecret=@ClientSecret,
                    IdempotencyKey=@IdempotencyKey,
                    Metadata=@Metadata,
                    CreatedAt=@CreatedAt,
                    UpdatedAt=@UpdatedAt,
                    ExpiresAt=@ExpiresAt,
                    ReservationId=@ReservationId
                    WHERE Id=@Id";
                AddParameters(cmd, intent);
                var rows = cmd.ExecuteNonQuery();
                return rows == 0
                    ? Fin<Unit>.Fail(Error.New($"PaymentIntent {intent.Id} not found"))
                    : FinSucc(unit);
            }
            catch (Exception ex)
            {
                return Fin<Unit>.Fail(Error.New(ex));
            }
        });

    public TraceableT<Fin<PaymentIntent>> InsertPaymentIntent(PaymentIntent intent) =>
        TraceFin("db.payment_intents.insert", () =>
        {
            try
            {
                using var conn = new SqliteConnection(_env.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO PaymentIntents (
                    Id, CartId, UserId, Provider, ProviderIntentId, Currency, Amount, Status,
                    ClientSecret, IdempotencyKey, Metadata, CreatedAt, UpdatedAt, ExpiresAt, ReservationId)
                    VALUES (@Id, @CartId, @UserId, @Provider, @ProviderIntentId, @Currency, @Amount, @Status,
                            @ClientSecret, @IdempotencyKey, @Metadata, @CreatedAt, @UpdatedAt, @ExpiresAt, @ReservationId);";
                AddParameters(cmd, intent);
                cmd.ExecuteNonQuery();
                return FinSucc(intent);
            }
            catch (Exception ex)
            {
                return Fin<PaymentIntent>.Fail(Error.New(ex));
            }
        });

    public TraceableT<Fin<Customer>> GetCustomerById(Guid userId) =>
        TraceFin("db.customer.get_by_id", () => FinSucc(new Customer(userId, "Unknown", "unknown@example.com")));

    public TraceableT<Fin<Cart>> GetCustomerCart(Guid userId) =>
        TraceFin("db.cart.get_by_user", () =>
        {
            if (userId == Guid.Empty)
                return Fin<Cart>.Fail(Error.New("Cannot create a Cart for non-existent customer"));
            return FinSucc(Cart.Empty(userId));
        });

    // ----------------- Helpers -----------------
    private static void AddParameters(SqliteCommand cmd, PaymentIntent intent)
    {
        string? clientSecretOrNull = intent.ClientSecret.Match<string?>(s => s, () => null);
        string? idempotencyOrNull = intent.IdempotencyKey.Match<string?>(s => s, () => null);
        object expiresDb = intent.ExpiresAt.Match<object>(dt => dt.UtcDateTime.ToString("O"), () => DBNull.Value);
        string metadata = SerializeMetadata(intent.Metadata);

        cmd.Parameters.AddWithValue("@Id", intent.Id.ToString());
        cmd.Parameters.AddWithValue("@CartId", intent.CartId.ToString());
        cmd.Parameters.AddWithValue("@UserId", intent.UserId.ToString());
        cmd.Parameters.AddWithValue("@Provider", intent.Provider);
        cmd.Parameters.AddWithValue("@ProviderIntentId", intent.ProviderIntentId);
        cmd.Parameters.AddWithValue("@Currency", intent.Currency);
        cmd.Parameters.AddWithValue("@Amount", intent.Amount);
        cmd.Parameters.AddWithValue("@Status", (int)intent.Status);
        cmd.Parameters.AddWithValue("@ClientSecret", (object?)clientSecretOrNull ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IdempotencyKey", (object?)idempotencyOrNull ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Metadata", metadata);
        cmd.Parameters.AddWithValue("@CreatedAt", intent.CreatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", intent.UpdatedAt.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@ExpiresAt", expiresDb);
        cmd.Parameters.AddWithValue("@ReservationId", intent.ReservationId.ToString());
    }

    private static string SerializeMetadata(Map<string, string> map) =>
        string.Join(";", map.AsEnumerable().Select(kv => $"{kv.Key}={kv.Value}"));

    private static PaymentIntent ReadIntent(SqliteDataReader reader)
    {
        Guid ParseGuid(int ordinal) => Guid.Parse(reader.GetString(ordinal));
        string? GetNullableString(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        var id = ParseGuid(reader.GetOrdinal("Id"));
        var cartId = ParseGuid(reader.GetOrdinal("CartId"));
        var userId = ParseGuid(reader.GetOrdinal("UserId"));
        var provider = reader.GetString(reader.GetOrdinal("Provider"));
        var providerIntentId = reader.GetString(reader.GetOrdinal("ProviderIntentId"));
        var currency = reader.GetString(reader.GetOrdinal("Currency"));
        var amount = reader.GetDecimal(reader.GetOrdinal("Amount"));
        var status = (PaymentIntentStatus)reader.GetInt32(reader.GetOrdinal("Status"));
        var clientSecret = GetNullableString(reader.GetOrdinal("ClientSecret"));
        var idempotencyKey = GetNullableString(reader.GetOrdinal("IdempotencyKey"));
        var metadataRaw = GetNullableString(reader.GetOrdinal("Metadata")) ?? string.Empty;
        var createdAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")));
        var updatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")));
        var expiresAtStr = GetNullableString(reader.GetOrdinal("ExpiresAt"));
        var reservationId = ParseGuid(reader.GetOrdinal("ReservationId"));

        var metaPairs = metadataRaw.Length == 0
            ? Enumerable.Empty<KeyValuePair<string, string>>()
            : metadataRaw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Split('=', 2))
                         .Where(a => a.Length == 2)
                         .Select(a => new KeyValuePair<string, string>(a[0], a[1]));

        return new PaymentIntent(
            Id: id,
            CartId: cartId,
            UserId: userId,
            Provider: provider,
            ProviderIntentId: providerIntentId,
            Currency: currency,
            Amount: amount,
            Status: status,
            ClientSecret: Optional(clientSecret),
            IdempotencyKey: Optional(idempotencyKey),
            Metadata: metaPairs.ToMap(),
            CreatedAt: createdAt,
            UpdatedAt: updatedAt,
            ExpiresAt: Optional(expiresAtStr)
                .Filter(str => !string.IsNullOrWhiteSpace(str))
                .Map(DateTimeOffset.Parse),
            ReservationId: reservationId
        );
    }
}
