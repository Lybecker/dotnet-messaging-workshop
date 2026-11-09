using Microsoft.Data.SqlClient;

namespace Contoso.Messaging.Reliability;

/// <summary>
/// TRANSACTIONAL OUTBOX – solves the "dual write" problem:
///   save order to DB ✔  →  publish OrderPlaced ✘ (broker down / process crash)  ⇒ lost event
///   publish ✔ → DB commit ✘                                                   ⇒ ghost event
/// Fix: write the outgoing message to an Outbox table in the SAME DB transaction as the
/// business change; a relay publishes it afterwards and marks it as sent.
/// Result: at-least-once publishing (the relay may publish twice) → consumers must be idempotent,
/// and a deterministic MessageId lets the broker (ASB duplicate detection) drop relay duplicates.
///
///   CREATE TABLE dbo.Outbox (Id nvarchar(200) PRIMARY KEY, Destination nvarchar(200), Type nvarchar(200),
///                            CorrelationId nvarchar(200), Body varbinary(max), CreatedAt datetimeoffset,
///                            SentAt datetimeoffset NULL);
///
/// Wolverine and Rebus ship this out of the box – this is what they do under the hood.
/// </summary>
public sealed class SqlOutbox(string connectionString)
{
    public static async Task EnqueueAsync(SqlConnection conn, SqlTransaction tx,
        string messageId, string destination, string type, string correlationId, byte[] body, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("""
            INSERT INTO dbo.Outbox (Id, Destination, Type, CorrelationId, Body, CreatedAt)
            VALUES (@id, @dest, @type, @corr, @body, sysdatetimeoffset())
            """, conn, tx);
        cmd.Parameters.AddWithValue("@id", messageId);
        cmd.Parameters.AddWithValue("@dest", destination);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@corr", correlationId);
        cmd.Parameters.AddWithValue("@body", body);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public delegate Task Publish(string messageId, string destination, string type, string correlationId, byte[] body, CancellationToken ct);

    /// <summary>Relay loop: poll unsent rows in order, publish, mark sent. Run ONE active relay (or lock rows).</summary>
    public async Task RelayAsync(Publish publish, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            var batch = new List<(string Id, string Dest, string Type, string Corr, byte[] Body)>();
            await using (var q = new SqlCommand(
                "SELECT TOP 100 Id, Destination, Type, CorrelationId, Body FROM dbo.Outbox WITH (READPAST, UPDLOCK) WHERE SentAt IS NULL ORDER BY CreatedAt", conn))
            await using (var r = await q.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    batch.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), (byte[])r[4]));

            foreach (var m in batch)
            {
                await publish(m.Id, m.Dest, m.Type, m.Corr, m.Body, ct);     // may duplicate on crash here
                await using var done = new SqlCommand("UPDATE dbo.Outbox SET SentAt = sysdatetimeoffset() WHERE Id = @id", conn);
                done.Parameters.AddWithValue("@id", m.Id);
                await done.ExecuteNonQueryAsync(ct);
            }
            if (batch.Count == 0) await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}
