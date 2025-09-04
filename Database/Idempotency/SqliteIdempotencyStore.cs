using Microsoft.Data.Sqlite;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Monads;
using System.Text.Json;

namespace SmallShopBigAmbitions.Database.Idempotency;

public sealed class SqliteIdempotencyStore(string connectionString) : IIdempotencyStore
{
    private readonly string _cs = connectionString;

    private const string TableName = "IdempotencyLocks";

    public IO<Fin<IdemLookup<string>>> TryAcquire(
        string scope,
        string key,
        string fingerprint,
        TimeSpan ttl,
        CancellationToken ct = default
    ) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_cs);
        conn.Open();
        EnsureSchema(conn);

        using var tx = conn.BeginTransaction();

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);

        // 1) Try to insert a fresh lock. If inserted, we acquired it.
        using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = $@"
INSERT OR IGNORE INTO {TableName}
    (Scope, Key, Fingerprint, Status, Response, ExpiresAt, CreatedAt, UpdatedAt)
VALUES
    (@Scope, @Key, @Fingerprint, 0, NULL, @ExpiresAt, @Now, @Now);";
            insertCmd.Parameters.AddWithValue("@Scope", scope);
            insertCmd.Parameters.AddWithValue("@Key", key);
            insertCmd.Parameters.AddWithValue("@Fingerprint", fingerprint);
            insertCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt.ToString("O"));
            insertCmd.Parameters.AddWithValue("@Now", now.ToString("O"));

            var inserted = insertCmd.ExecuteNonQuery();
            if (inserted == 1)
            {
                tx.Commit();
                return new IdemLookup<string>(IdempotencyState.Acquired, null);
            }
        }

        // 2) Row exists; check current state
        using (var selectCmd = conn.CreateCommand())
        {
            selectCmd.Transaction = tx;
            selectCmd.CommandText = $@"SELECT Fingerprint, Status, Response, ExpiresAt FROM {TableName}
WHERE Scope = @Scope AND Key = @Key";
            selectCmd.Parameters.AddWithValue("@Scope", scope);
            selectCmd.Parameters.AddWithValue("@Key", key);

            using var reader = selectCmd.ExecuteReader();
            if (!reader.Read())
            {
                // Should not happen because INSERT OR IGNORE failed due to duplicate; but handle gracefully
                // Retry insert once
                using var retryCmd = conn.CreateCommand();
                retryCmd.Transaction = tx;
                retryCmd.CommandText = $@"
INSERT OR IGNORE INTO {TableName}
    (Scope, Key, Fingerprint, Status, Response, ExpiresAt, CreatedAt, UpdatedAt)
VALUES
    (@Scope, @Key, @Fingerprint, 0, NULL, @ExpiresAt, @Now, @Now);";
                retryCmd.Parameters.AddWithValue("@Scope", scope);
                retryCmd.Parameters.AddWithValue("@Key", key);
                retryCmd.Parameters.AddWithValue("@Fingerprint", fingerprint);
                retryCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt.ToString("O"));
                retryCmd.Parameters.AddWithValue("@Now", now.ToString("O"));
                var inserted2 = retryCmd.ExecuteNonQuery();
                tx.Commit();
                return inserted2 == 1
                    ? new IdemLookup<string>(IdempotencyState.Acquired, null)
                    : new IdemLookup<string>(IdempotencyState.DuplicateSameBusy, null);
            }

            var existingFingerprint = reader.GetString(0);
            var status = reader.GetInt32(1); // 0=Processing, 1=Completed
            var response = reader.IsDBNull(2) ? null : reader.GetString(2);
            var expiresAtStr = reader.GetString(3);
            var existingExpiresAt = DateTimeOffset.Parse(expiresAtStr);

            // 3) If expired, take over the lock
            if (existingExpiresAt <= now)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = $@"UPDATE {TableName}
SET Fingerprint = @Fingerprint, Status = 0, Response = NULL, ExpiresAt = @ExpiresAt, UpdatedAt = @Now
WHERE Scope = @Scope AND Key = @Key";
                updateCmd.Parameters.AddWithValue("@Fingerprint", fingerprint);
                updateCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt.ToString("O"));
                updateCmd.Parameters.AddWithValue("@Now", now.ToString("O"));
                updateCmd.Parameters.AddWithValue("@Scope", scope);
                updateCmd.Parameters.AddWithValue("@Key", key);
                updateCmd.ExecuteNonQuery();

                tx.Commit();
                return new IdemLookup<string>(IdempotencyState.Acquired, null);
            }

            // 4) Not expired; evaluate duplicates
            if (string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
            {
                if (status == 1 && !string.IsNullOrWhiteSpace(response))
                {
                    tx.Commit();
                    return new IdemLookup<string>(IdempotencyState.DuplicateSameDone, response);
                }
                else
                {
                    tx.Commit();
                    return new IdemLookup<string>(IdempotencyState.DuplicateSameBusy, null);
                }
            }
            else
            {
                tx.Commit();
                return new IdemLookup<string>(IdempotencyState.DuplicateDifferent, null);
            }
        }
    });

    public IO<Fin<Unit>> Complete<T>(
        string scope,
        string key,
        T response,
        CancellationToken ct = default
    ) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_cs);
        conn.Open();
        EnsureSchema(conn);

        var now = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(response);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"UPDATE {TableName}
SET Status = 1, Response = @Response, UpdatedAt = @Now
WHERE Scope = @Scope AND Key = @Key";
        cmd.Parameters.AddWithValue("@Response", json);
        cmd.Parameters.AddWithValue("@Now", now.ToString("O"));
        cmd.Parameters.AddWithValue("@Scope", scope);
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.ExecuteNonQuery();

        return unit;
    });

    public IO<Fin<Unit>> Abandon(
        string scope,
        string key,
        CancellationToken ct = default
    ) => IOFin.From(() =>
    {
        using var conn = new SqliteConnection(_cs);
        conn.Open();
        EnsureSchema(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {TableName} WHERE Scope = @Scope AND Key = @Key";
        cmd.Parameters.AddWithValue("@Scope", scope);
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.ExecuteNonQuery();

        return unit;
    });

    private static void EnsureSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TableName} (
    Scope TEXT NOT NULL,
    Key TEXT NOT NULL,
    Fingerprint TEXT NOT NULL,
    Status INTEGER NOT NULL, -- 0 Processing, 1 Completed
    Response TEXT NULL,
    ExpiresAt TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    PRIMARY KEY (Scope, Key)
);";
        cmd.ExecuteNonQuery();
    }
}